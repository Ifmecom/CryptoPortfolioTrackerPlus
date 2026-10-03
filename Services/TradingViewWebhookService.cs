using System.Net.Http;
using System.Text;
using CryptoPortfolioTracker.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoPortfolioTracker.Services;

/// <inheritdoc cref="ITradingViewWebhookService"/>
public sealed class TradingViewWebhookService : ITradingViewWebhookService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(TradingViewWebhookService).PadRight(22));
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);
    private const string WebhookTag = "[TV]";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly Settings _settings;
    private readonly INotifierService? _notifier;
    private readonly List<TradingViewAlert> _recent = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public TradingViewWebhookService(Settings settings, INotifierService? notifier = null)
    {
        _settings = settings;
        _notifier = notifier;
    }

    public string Status { get; private set; } = "Uit";

    public IReadOnlyList<TradingViewAlert> RecentAlerts
    {
        get { lock (_recent) return _recent.ToList(); }
    }

    public event EventHandler? StateChanged;

    // ── Lus ──────────────────────────────────────────────────────────────────

    public void Start()
    {
        if (_loop is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            do { await PollNowAsync(ct); }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { /* afsluiten */ }
    }

    public async Task PollNowAsync(CancellationToken ct = default)
    {
        if (!_settings.IsTradingViewWebhookEnabled)
        {
            SetStatus("Uit — zet 'Webhook-alerts ontvangen' aan in Instellingen → TradingView.");
            return;
        }
        var topic = _settings.TradingViewWebhookTopic;
        if (!TradingViewAlerts.IsValidTopic(topic))
        {
            SetStatus("Nog geen geheime webhook-URL — klik 'Nieuwe URL' in Instellingen → TradingView.");
            return;
        }
        if (!await _gate.WaitAsync(0, ct)) return;   // er loopt al een ronde

        try
        {
            var body = await Http.GetStringAsync(TradingViewAlerts.PollUrl(topic, _settings.TradingViewWebhookLastId), ct);
            var alerts = TradingViewAlerts.ParsePoll(body);
            foreach (var a in alerts)
            {
                await HandleAsync(a, ct);
                _settings.TradingViewWebhookLastId = a.Id;
            }

            var last = RecentAlerts.FirstOrDefault();
            SetStatus(last is null
                ? $"Luistert (elke {PollInterval.TotalSeconds:0} s) — nog geen alerts ontvangen."
                : $"Luistert — laatste alert: {TradingViewAlerts.Describe(last)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradingView-webhook: ophalen mislukt");
            SetStatus($"Ophalen mislukt ({ex.Message}) — probeert het over {PollInterval.TotalSeconds:0} s opnieuw.");
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> SendTestAsync(CancellationToken ct = default)
    {
        var topic = _settings.TradingViewWebhookTopic;
        if (!TradingViewAlerts.IsValidTopic(topic)) return false;
        try
        {
            var json = "{\"bron\":\"CPT-test\",\"event\":\"test\",\"ticker\":\"TEST\"}";
            using var resp = await Http.PostAsync(TradingViewAlerts.WebhookUrl(topic),
                new StringContent("Testbericht vanuit Crypto Portfolio Tracker " + json, Encoding.UTF8, "text/plain"), ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradingView-webhook: testbericht mislukt");
            return false;
        }
    }

    // ── Eén alert ────────────────────────────────────────────────────────────

    private async Task HandleAsync(TradingViewAlert alert, CancellationToken ct)
    {
        Logger.Information("TradingView-alert {Event} {Pair} @ {Price} ({Id})", alert.Event, alert.Pair, alert.Price, alert.Id);
        lock (_recent)
        {
            _recent.Insert(0, alert);
            if (_recent.Count > 50) _recent.RemoveRange(50, _recent.Count - 50);
        }

        WatchedSetup? setup = null;
        string? info = null;
        try
        {
            setup = await FindSetupAsync(alert, ct);
            if (setup is not null)
                info = $"Gevolgde setup: {setup.CoinName} {setup.Direction} · entry {setup.EntryPrice:0.########} · SL {setup.StopLoss:0.########} · TP1 {setup.Target1:0.########} ({setup.Status})";

            if (alert.Event == "entry")
            {
                var order = await TryAutoOrderAsync(alert, setup, ct);
                if (order is not null) info = (info is null ? "" : info + "\n") + order;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradingView-alert: verwerken mislukt voor {Pair}", alert.Pair);
        }

        if (_notifier is not null)
        {
            try { await _notifier.SendAlertAsync(TradingViewAlerts.FormatTelegram(alert, info), ct); }
            catch (Exception ex) { Logger.Debug(ex, "TradingView-alert: Telegram mislukt"); }
        }
    }

    private static PortfolioService Portfolio => App.Container.GetRequiredService<PortfolioService>();

    private async Task<WatchedSetup?> FindSetupAsync(TradingViewAlert alert, CancellationToken ct)
    {
        if (alert.Pair.Length == 0) return null;
        var ctx = Portfolio.Context;
        if (ctx is null) return null;

        var symbol = TradingViewAlerts.BaseSymbol(alert.Pair, "USDT", "USDC", _settings.BybitQuoteCoin, "FDUSD", "BUSD", "USD", "EUR", "BTC");
        var active = await ctx.WatchedSetups.AsNoTracking()
            .Where(s => s.Status == WatchedSetupStatus.Watching || s.Status == WatchedSetupStatus.Open)
            .ToListAsync(ct);
        return active
            .Where(s => string.Equals(s.CoinSymbol.Trim().TrimStart('$'), symbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.AddedAt)
            .FirstOrDefault();
    }

    /// <summary>Plaatst (als toegestaan) een Bybit Demo-limitorder op de entry van de gevolgde setup. Geeft een regel voor Telegram terug.</summary>
    private async Task<string?> TryAutoOrderAsync(TradingViewAlert alert, WatchedSetup? setup, CancellationToken ct)
    {
        if (!_settings.IsTradingViewWebhookAutoOrder) return null;

        var ctx = Portfolio.Context;
        if (ctx is null) return null;

        string quote = _settings.BybitQuoteCoin;
        var demoOrders = await ctx.ExchangeOrders.AsNoTracking()
            .Where(o => !o.IsPaper && o.Exchange == ExchangeKind.BybitDemo)
            .Select(o => new { o.Symbol, o.Status, o.CreatedAt, o.Notes })
            .ToListAsync(ct);
        int placedToday = demoOrders.Count(o => o.CreatedAt >= DateTime.UtcNow.Date && (o.Notes ?? "").Contains(AutoTraderService.AutoTag));
        bool hasOpen = setup is not null && demoOrders.Any(o =>
            o.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled or OrderStatus.Filled &&
            string.Equals(o.Symbol.EndsWith(quote, StringComparison.OrdinalIgnoreCase) ? o.Symbol[..^quote.Length] : o.Symbol,
                          setup.CoinSymbol.Trim().TrimStart('$'), StringComparison.OrdinalIgnoreCase));

        var (place, reason) = TradingViewAlerts.AutoOrderCheck(alert, setup, true, hasOpen, placedToday, _settings.AutoTradeMaxPerDay);
        if (!place || setup is null)
        {
            Logger.Information("TradingView-alert {Pair}: geen demo-order — {Reason}", alert.Pair, reason);
            return $"Geen demo-order: {reason}.";
        }

        try
        {
            var coin = await ctx.Coins.AsNoTracking().FirstOrDefaultAsync(c => c.ApiId == setup.CoinApiId, ct);
            if (coin is null) return "Geen demo-order: munt niet gevonden in de bibliotheek.";

            var live = App.Container.GetRequiredService<ILiveOrderExecutor>();
            var trade = App.Container.GetRequiredService<ITradeService>();

            double capital = (double)await live.GetAvailableQuoteAsync(ct);
            var size = PositionSizeCalculator.Suggest(capital, _settings.AutoTradeRiskPct, setup.EntryPrice, setup.StopLoss);
            double amount = Math.Min(size.Amount, capital * _settings.AutoTradeMaxPositionPct / 100.0);
            if (!size.IsValid || amount <= 0) return "Geen demo-order: geen geldige positiegrootte (saldo of stop-loss).";

            var req = new OrderRequest(
                ExchangeKind.BybitDemo, OrderSide.Buy, MarketType.Spot, OrderType.Limit,
                Math.Round(amount, 2), setup.EntryPrice, setup.StopLoss, setup.Target1, 0, 1,
                Notes: $"{AutoTraderService.AutoTag} {WebhookTag} TradingView-alert entry · setup #{setup.Id} · risico {_settings.AutoTradeRiskPct:0.#}%",
                WatchedSetupId: setup.Id);
            var signal = new Signal
            {
                CoinId    = coin.Id,
                CreatedAt = DateTime.UtcNow,
                Direction = SignalDirection.Long,
                Reasoning = $"TradingView-alert 'Entry geraakt' voor gevolgde setup #{setup.Id}",
            };

            var order = await trade.PlaceLiveAsync(coin, signal, req);
            try { await App.Container.GetRequiredService<IWatchedSetupService>().LinkOrderAsync(setup.Id, order.Id); }
            catch (Exception ex) { Logger.Warning(ex, "TradingView-alert: order {OrderId} niet aan setup {SetupId} gekoppeld", order.Id, setup.Id); }
            Logger.Information("TradingView-alert {Pair}: demo-order {OrderId} geplaatst", alert.Pair, order.Id);
            return $"🤖 Demo-order geplaatst: {order.Qty:0.########} {setup.CoinSymbol} ({amount:0} {quote}, limit @ {setup.EntryPrice:0.########}) · SL {setup.StopLoss:0.########} · TP {setup.Target1:0.########}";
        }
        catch (Exception ex)
        {
            Logger.Information("TradingView-alert {Pair}: demo-order niet geplaatst — {Msg}", alert.Pair, ex.Message);
            return $"Demo-order niet geplaatst: {ex.Message}";
        }
    }

    private void SetStatus(string status)
    {
        Status = status;
        try { StateChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { Logger.Debug(ex, "TradingView-webhook: StateChanged-handler faalde"); }
    }
}
