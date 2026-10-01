using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Order-uitvoering op Bybit EU Demo Trading (v1.47) — spot, alleen Long, met stop-loss en take-profit
/// direct gekoppeld aan de instaporder zodat Bybit ze bewaakt (ook als de pc uit staat).
///
/// De rekenkern is puur en getest: <see cref="BybitOrderPlanner"/> (order opbouwen/afronden),
/// <see cref="LiveOrderReconciler"/> (vullingen en sluitingen verwerken) en <see cref="BybitApi"/>
/// (ondertekenen en parsen). Deze klasse doet alleen HTTP en database.
/// </summary>
public class BybitDemoExecutor : ILiveOrderExecutor
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(BybitDemoExecutor).PadRight(22));

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly SemaphoreSlim _syncGate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (BybitSpotInstrument Inst, DateTime At)> _instruments = new();
    private static readonly TimeSpan InstrumentTtl = TimeSpan.FromHours(6);

    /// <summary>Executions worden per venster van max. 7 dagen opgevraagd (Bybit-limiet).</summary>
    private static readonly TimeSpan ExecWindow = TimeSpan.FromDays(7);
    private const int MaxExecWindows = 8;

    private readonly PortfolioService        _portfolio;
    private readonly IExchangeAccountService _accounts;
    private readonly Settings                _settings;
    private readonly INotifierService?       _notifier;

    public BybitDemoExecutor(
        PortfolioService        portfolio,
        IExchangeAccountService accounts,
        Settings                settings,
        INotifierService?       notifier = null)
    {
        _portfolio = portfolio;
        _accounts  = accounts;
        _settings  = settings;
        _notifier  = notifier;
    }

    public ExchangeKind Exchange => ExchangeKind.BybitDemo;

    // =========================================================================
    // Plaatsen
    // =========================================================================

    public async Task<ExchangeOrder> PlaceAsync(Coin coin, Signal signal, OrderRequest req, CancellationToken ct = default)
    {
        var ctx = _portfolio.Context ?? throw new InvalidOperationException("Geen database beschikbaar.");
        var api = await GetApiAsync();

        string symbol = BybitOrderPlanner.SymbolFor(coin.Symbol, _settings.BybitQuoteCoin);

        bool alreadyOpen = await ctx.ExchangeOrders.AsNoTracking().AnyAsync(o =>
            !o.IsPaper && o.Exchange == ExchangeKind.BybitDemo && o.Symbol == symbol
            && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyFilled || o.Status == OrderStatus.Filled), ct);
        if (alreadyOpen)
            throw new InvalidOperationException($"Er loopt al een demo-order of -positie op {symbol}. Sluit of annuleer die eerst.");

        var inst = await GetInstrumentAsync(api, symbol, ct)
            ?? throw new InvalidOperationException($"{symbol} is niet beschikbaar op Bybit EU (spot). Probeer een andere munt.");

        decimal ask = await GetAskAsync(api, symbol, ct);
        var plan = BybitOrderPlanner.Plan(req, ask, inst, BybitOrderPlanner.NewOrderLinkId());
        if (!plan.IsValid)
            throw new InvalidOperationException(string.Join(" ", plan.Errors));

        var (code, msg, result) = await PostAsync(api, "/v5/order/create", plan.JsonBody, r => BybitApi.Str(r, "orderId"), ct);
        if (code != 0)
            throw new InvalidOperationException($"Bybit weigerde de order: {BybitApi.ExplainError(code, msg)}");

        var notes = new StringBuilder(req.Notes ?? string.Empty);
        if (notes.Length > 0) notes.AppendLine();
        notes.Append($"Bybit Demo · orderId {result}");
        foreach (var w in plan.Warnings) notes.Append($"\n⚠ {w}");

        var order = new ExchangeOrder
        {
            SignalId        = signal.Id > 0 ? signal.Id : null,
            Exchange        = ExchangeKind.BybitDemo,
            Symbol          = symbol,
            Side            = OrderSide.Buy,
            Type            = req.OrderType,
            MarketType      = MarketType.Spot,
            Leverage        = 1,
            Qty             = (double)plan.Qty,
            Entry           = (double)(plan.IsMarketEmulated ? ask : plan.Price),
            StopLoss        = (double)plan.StopLoss,
            TakeProfit      = (double)plan.TakeProfit,
            TakeProfit2     = 0,
            Tp1ClosePct     = 100,
            Tp2ClosePct     = 100,
            Status          = OrderStatus.Pending,
            ExternalOrderId = plan.OrderLinkId,
            IsPaper         = false,
            CreatedAt       = DateTime.UtcNow,
            Notes           = notes.ToString(),
            WatchedSetupId  = req.WatchedSetupId,
        };
        ctx.ExchangeOrders.Add(order);
        await ctx.SaveChangesAsync(ct);

        Logger.Information(
            "BybitDemo: order {Link} {Symbol} Buy qty={Qty} @ {Price} (market={Market}) SL={SL} TP={TP}",
            plan.OrderLinkId, symbol, plan.Qty, plan.Price, plan.IsMarketEmulated, plan.StopLoss, plan.TakeProfit);

        // Een (nagebootste) market-order vult meestal direct — meteen bijwerken.
        try
        {
            await Task.Delay(1500, ct);
            await SyncOrdersAsync(api, new List<ExchangeOrder> { order }, ct);
        }
        catch (Exception ex) { Logger.Debug(ex, "BybitDemo: directe sync na plaatsen mislukt"); }

        return order;
    }

    // =========================================================================
    // Annuleren en sluiten
    // =========================================================================

    public async Task<bool> CancelAsync(ExchangeOrder order, CancellationToken ct = default)
    {
        var ctx = _portfolio.Context ?? throw new InvalidOperationException("Geen database beschikbaar.");
        var api = await GetApiAsync();

        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["category"] = "spot", ["symbol"] = order.Symbol, ["orderLinkId"] = order.ExternalOrderId,
        });
        var (code, msg, _) = await PostAsync(api, "/v5/order/cancel", body, _ => true, ct);
        if (code != 0)
            Logger.Information("BybitDemo: annuleren {Link} gaf {Code} {Msg} — status wordt gesynchroniseerd",
                order.ExternalOrderId, code, msg);

        // Status altijd opnieuw ophalen: de order kan inmiddels (deels) gevuld zijn.
        var tracked = await ctx.ExchangeOrders.FirstAsync(o => o.Id == order.Id, ct);
        await SyncOrdersAsync(api, new List<ExchangeOrder> { tracked }, ct);

        if (tracked.Status is OrderStatus.Pending)
        {
            tracked.Status = OrderStatus.Cancelled;
            await ctx.SaveChangesAsync(ct);
        }
        order.Status = tracked.Status;
        return tracked.Status == OrderStatus.Cancelled;
    }

    public async Task CloseAsync(ExchangeOrder order, CancellationToken ct = default)
    {
        var ctx = _portfolio.Context ?? throw new InvalidOperationException("Geen database beschikbaar.");
        var api = await GetApiAsync();

        var tracked = await ctx.ExchangeOrders.FirstAsync(o => o.Id == order.Id, ct);
        if (tracked.Status != OrderStatus.Filled)
            throw new InvalidOperationException("Alleen een gevulde (open) positie kan worden gesloten.");

        var inst = await GetInstrumentAsync(api, tracked.Symbol, ct)
            ?? throw new InvalidOperationException($"{tracked.Symbol} niet gevonden op Bybit.");

        // 1. Gekoppelde TP/SL en open orders op dit paar weghalen (die houden de munten vast).
        foreach (var filter in new[] { "tpslOrder", "StopOrder", "Order" })
        {
            var cancelBody = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["category"] = "spot", ["symbol"] = tracked.Symbol, ["orderFilter"] = filter,
            });
            var (c, m, _) = await PostAsync(api, "/v5/order/cancel-all", cancelBody, _ => true, ct);
            if (c != 0) Logger.Debug("BybitDemo: cancel-all {Filter} → {Code} {Msg}", filter, c, m);
        }
        await Task.Delay(800, ct);

        // 2. Verkoop wat er van deze positie beschikbaar is.
        decimal available = await GetSpotAvailableAsync(api, tracked.Symbol, buy: false, ct);
        decimal qty = BybitOrderPlanner.RoundDown(Math.Min((decimal)tracked.Qty, available), inst.BasePrecision);
        if (qty <= 0 || qty < inst.MinOrderQty)
            throw new InvalidOperationException(
                $"Geen verkoopbare {inst.BaseCoin} gevonden (beschikbaar: {BybitApi.Num(available)}). Controleer de positie op Bybit.");

        var sellBody = BybitOrderPlanner.BuildMarketSellBody(tracked.Symbol, qty, BybitOrderPlanner.NewOrderLinkId("cls"));
        var (code, msg, _) = await PostAsync(api, "/v5/order/create", sellBody, _ => true, ct);
        if (code != 0)
            throw new InvalidOperationException(
                $"Verkopen mislukt: {BybitApi.ExplainError(code, msg)}. Let op: de TP/SL-orders zijn al weggehaald.");

        Logger.Information("BybitDemo: positie {Symbol} handmatig gesloten, verkocht {Qty}", tracked.Symbol, qty);

        // 3. Sluiting verwerken.
        await Task.Delay(1500, ct);
        await SyncOrdersAsync(api, new List<ExchangeOrder> { tracked }, ct);
        order.Status = tracked.Status;
    }

    // =========================================================================
    // Synchroniseren
    // =========================================================================

    public async Task<IReadOnlyList<string>> SyncAsync(CancellationToken ct = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null) return Array.Empty<string>();

        var open = await ctx.ExchangeOrders
            .Where(o => !o.IsPaper && o.Exchange == ExchangeKind.BybitDemo
                     && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyFilled || o.Status == OrderStatus.Filled))
            .ToListAsync(ct);
        if (open.Count == 0) return Array.Empty<string>();

        var api = await GetApiAsync();
        return await SyncOrdersAsync(api, open, ct);
    }

    private async Task<IReadOnlyList<string>> SyncOrdersAsync(ApiContext api, List<ExchangeOrder> orders, CancellationToken ct)
    {
        var ctx = _portfolio.Context;
        if (ctx is null || orders.Count == 0) return Array.Empty<string>();

        if (!await _syncGate.WaitAsync(TimeSpan.FromSeconds(30), ct))
            return Array.Empty<string>();

        var events = new List<string>();
        try
        {
            foreach (var group in orders.GroupBy(o => o.Symbol))
            {
                var inst = await GetInstrumentAsync(api, group.Key, ct);
                string baseCoin = inst?.BaseCoin ?? group.Key.Replace(_settings.BybitQuoteCoin, string.Empty);

                var since = group.Min(o => o.CreatedAt).AddMinutes(-1);
                var execs = await GetExecutionsAsync(api, group.Key, since, ct);

                foreach (var order in group)
                {
                    BybitOrderInfo? info = null;
                    if (order.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled)
                        info = await GetOrderAsync(api, group.Key, order.ExternalOrderId, ct);

                    var update = LiveOrderReconciler.Reconcile(order, info, execs, baseCoin);
                    if (!update.Changed) continue;

                    LiveOrderReconciler.Apply(order, update);
                    events.Add($"{baseCoin}: {update.Event}");
                    Logger.Information("BybitDemo: {Symbol} #{Id} {Event}", order.Symbol, order.Id, update.Event);
                }
            }

            await ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "BybitDemo: synchroniseren mislukt");
            events.Add($"⚠ Synchroniseren met Bybit mislukt: {ex.Message}");
        }
        finally
        {
            _syncGate.Release();
        }

        if (_notifier is not null && events.Count > 0 && events.Any(e => !e.StartsWith("⚠")))
        {
            try { await _notifier.SendAlertAsync("<b>🧪 Bybit Demo</b>\n" + string.Join("\n", events), ct); }
            catch (Exception ex) { Logger.Debug(ex, "BybitDemo: Telegram-melding mislukt"); }
        }
        return events;
    }

    public async Task<decimal> GetAvailableQuoteAsync(CancellationToken ct = default)
    {
        var api = await GetApiAsync();
        // Besteedbaar quote-saldo is voor elk paar met die quote gelijk; BTC is altijd genoteerd.
        return await GetSpotAvailableAsync(api, BybitOrderPlanner.SymbolFor("BTC", _settings.BybitQuoteCoin), buy: true, ct);
    }

    // =========================================================================
    // Bybit-calls
    // =========================================================================

    private sealed record ApiContext(string BaseUrl, ExchangeCredentials Creds);

    private async Task<ApiContext> GetApiAsync()
    {
        var creds = await _accounts.GetCredentialsAsync(ExchangeKind.BybitDemo)
            ?? throw new InvalidOperationException(
                "Geen Bybit Demo-sleutel ingesteld. Ga naar Instellingen → Exchanges → Bybit Demo.");
        var baseUrl = _settings.BybitDemoBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Het demo-domein is nog niet bepaald. Klik eerst op 'Verbinding testen' bij Bybit Demo in Instellingen.");
        // Extra slot op echt geld: deze uitvoerder praat uitsluitend met een demo-domein.
        if (!IsDemoDomain(baseUrl))
            throw new InvalidOperationException($"'{baseUrl}' is geen Bybit demo-domein — order geweigerd.");
        return new ApiContext(baseUrl.TrimEnd('/'), creds);
    }

    /// <summary>True voor https://api-demo.bybit.* (en localhost voor tests).</summary>
    public static bool IsDemoDomain(string baseUrl)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
           && (uri.Host.StartsWith("api-demo.bybit.", StringComparison.OrdinalIgnoreCase)
               || uri.IsLoopback);

    private async Task<BybitSpotInstrument?> GetInstrumentAsync(ApiContext api, string symbol, CancellationToken ct)
    {
        if (_instruments.TryGetValue(symbol, out var hit) && DateTime.UtcNow - hit.At < InstrumentTtl)
            return hit.Inst;

        // Marktdata is openbaar en identiek aan live; demo-domein gebruiken voor consistentie.
        var (code, _, inst) = await GetAsync(api, "/v5/market/instruments-info",
            BybitApi.Query(("category", "spot"), ("symbol", symbol)),
            r => BybitApi.ParseSpotInstrument(r, symbol), signed: false, ct);
        if (code == 0 && inst is not null)
            _instruments[symbol] = (inst, DateTime.UtcNow);
        return inst;
    }

    private async Task<decimal> GetAskAsync(ApiContext api, string symbol, CancellationToken ct)
    {
        var (_, _, ask) = await GetAsync(api, "/v5/market/tickers",
            BybitApi.Query(("category", "spot"), ("symbol", symbol)),
            r => BybitApi.ParseAskPrice(r, symbol), signed: false, ct);
        return ask;
    }

    /// <summary>
    /// Verhandelbaar saldo zonder lenen via <c>/v5/order/spot-borrow-check</c>: bij Buy het quote-bedrag,
    /// bij Sell de hoeveelheid basismunt. (Bybit EU Demo kent <c>wallet-balance</c> niet.)
    /// </summary>
    private static async Task<decimal> GetSpotAvailableAsync(ApiContext api, string symbol, bool buy, CancellationToken ct)
    {
        var (code, msg, available) = await GetAsync(api, "/v5/order/spot-borrow-check",
            BybitApi.Query(("category", "spot"), ("symbol", symbol), ("side", buy ? "Buy" : "Sell")),
            r => BybitApi.ParseSpotAvailable(r, buy), signed: true, ct);
        if (code != 0)
            throw new InvalidOperationException($"Saldo ophalen mislukt: {BybitApi.ExplainError(code, msg)}");
        return available;
    }

    /// <summary>Zoekt een order op orderLinkId: eerst actief, dan recent gesloten, dan de historie.</summary>
    private async Task<BybitOrderInfo?> GetOrderAsync(ApiContext api, string symbol, string orderLinkId, CancellationToken ct)
    {
        foreach (var (path, openOnly) in new[] { ("/v5/order/realtime", "0"), ("/v5/order/realtime", "1"), ("/v5/order/history", (string?)null) })
        {
            var query = BybitApi.Query(("category", "spot"), ("symbol", symbol), ("orderLinkId", orderLinkId), ("openOnly", openOnly));
            var (code, msg, orders) = await GetAsync(api, path, query, BybitApi.ParseOrders, signed: true, ct);
            if (code != 0)
            {
                Logger.Debug("BybitDemo: {Path} {Link} → {Code} {Msg}", path, orderLinkId, code, msg);
                continue;
            }
            var found = orders?.FirstOrDefault(o => o.OrderLinkId == orderLinkId);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Alle fills op een paar sinds <paramref name="since"/>, in vensters van 7 dagen (met paginering).</summary>
    private async Task<List<BybitExecution>> GetExecutionsAsync(ApiContext api, string symbol, DateTime since, CancellationToken ct)
    {
        var all = new List<BybitExecution>();
        var start = DateTime.SpecifyKind(since, DateTimeKind.Utc);
        var now   = DateTime.UtcNow;
        if (now - start > ExecWindow * MaxExecWindows) start = now - ExecWindow * MaxExecWindows;

        while (start < now)
        {
            var end = start + ExecWindow < now ? start + ExecWindow : now;
            string cursor = string.Empty;
            for (int page = 0; page < 10; page++)
            {
                var query = BybitApi.Query(
                    ("category", "spot"), ("symbol", symbol),
                    ("startTime", new DateTimeOffset(start).ToUnixTimeMilliseconds().ToString()),
                    ("endTime",   new DateTimeOffset(end).ToUnixTimeMilliseconds().ToString()),
                    ("limit", "100"), ("cursor", cursor));
                var (code, msg, data) = await GetAsync(api, "/v5/execution/list", query,
                    r => (Items: BybitApi.ParseExecutions(r), Next: BybitApi.NextCursor(r)), signed: true, ct);
                if (code != 0)
                    throw new InvalidOperationException($"Fills ophalen mislukt: {BybitApi.ExplainError(code, msg)}");

                all.AddRange(data.Items);
                if (string.IsNullOrEmpty(data.Next) || data.Items.Count == 0) break;
                cursor = data.Next;
            }
            start = end;
        }

        return all.GroupBy(x => x.ExecId).Select(g => g.First()).ToList();
    }

    // ── HTTP ────────────────────────────────────────────────────────────────

    private static async Task<(int Code, string Msg, T? Data)> GetAsync<T>(
        ApiContext api, string path, string query, Func<JsonElement, T> map, bool signed, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{api.BaseUrl}{path}?{query}");
        if (signed) AddAuth(request, api.Creds, query);
        return await SendAsync(request, map, ct);
    }

    private static async Task<(int Code, string Msg, T? Data)> PostAsync<T>(
        ApiContext api, string path, string jsonBody, Func<JsonElement, T> map, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{api.BaseUrl}{path}")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        AddAuth(request, api.Creds, jsonBody);
        return await SendAsync(request, map, ct);
    }

    private static void AddAuth(HttpRequestMessage request, ExchangeCredentials creds, string payload)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        request.Headers.Add("X-BAPI-API-KEY",     creds.ApiKey);
        request.Headers.Add("X-BAPI-TIMESTAMP",   ts.ToString());
        request.Headers.Add("X-BAPI-SIGN",        BybitApi.Sign(creds.Secret, ts, creds.ApiKey, BybitApi.RecvWindow, payload));
        request.Headers.Add("X-BAPI-SIGN-TYPE",   "2");
        request.Headers.Add("X-BAPI-RECV-WINDOW", BybitApi.RecvWindow.ToString());
    }

    private static async Task<(int Code, string Msg, T? Data)> SendAsync<T>(
        HttpRequestMessage request, Func<JsonElement, T> map, CancellationToken ct)
    {
        try
        {
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
                return ((int)response.StatusCode, $"HTTP {(int)response.StatusCode} zonder inhoud", default);

            using var doc = JsonDocument.Parse(body);
            var (code, msg, result) = BybitApi.ReadEnvelope(doc);
            if (code != 0) return (code, msg, default);
            if (result is null) return (-2, "Antwoord zonder result", default);
            return (0, msg, map(result.Value));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Warning(ex, "BybitDemo: {Method} {Url} mislukt", request.Method, request.RequestUri);
            return (-1, ex.Message, default);
        }
    }
}
