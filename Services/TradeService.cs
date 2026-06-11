using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class TradeService : ITradeService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(TradeService).PadRight(22));

    private readonly PortfolioService  _portfolioService;
    private readonly IGuardrailService? _guardrails;
    private readonly INotifierService?  _notifier;

    // Per-dag flag zodat de verlieslimiet-alert maar één keer per dag wordt verstuurd.
    private static DateTime _lastDailyLossAlertDay = DateTime.MinValue;

    public TradeService(
        PortfolioService portfolioService,
        IGuardrailService? guardrails = null,
        INotifierService?  notifier   = null)
    {
        _portfolioService = portfolioService;
        _guardrails       = guardrails;
        _notifier         = notifier;
    }

    // -----------------------------------------------------------------------
    // Paper trading (Sprint 1.4)
    // -----------------------------------------------------------------------

    public async Task<ExchangeOrder> PlacePaperAsync(Coin coin, Signal signal, OrderRequest req)
    {
        var context = _portfolioService.Context
            ?? throw new InvalidOperationException("No DB context available.");

        if (coin.Price <= 0)
            throw new ArgumentException($"Coin '{coin.Name}' has no current price — cannot place paper order.");

        // ── Risk-guardrails (kill-switch, max posities, dagverlieslimiet) ────
        if (_guardrails is not null)
        {
            var verdict = await _guardrails.CheckNewTradeAsync();
            if (verdict.IsBlocked)
                throw new InvalidOperationException($"⛔ Geblokkeerd door risk-guardrails: {verdict.ReasonText}");
        }

        // Entry price: limit orders use the specified limit price; market orders use current price.
        var entry = req.OrderType == OrderType.Limit && req.LimitPrice > 0
            ? req.LimitPrice
            : coin.Price;

        // Guard: SL/TP must be directionally correct before we persist anything.
        var validation = TradeSetupValidator.Validate(
            req.Side, entry, req.StopLossPrice, req.TakeProfitPrice, req.TakeProfit2Price);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Error, nameof(req));

        // Effective amount including leverage for quantity calculation
        var effectiveAmount = req.AmountUsdt * req.Leverage;
        var qty = Math.Round(effectiveAmount / entry, 8);

        // Status: limit orders wait to fill; market orders fill instantly
        var status  = req.OrderType == OrderType.Market ? OrderStatus.Filled : OrderStatus.Pending;
        var filledAt = req.OrderType == OrderType.Market ? (DateTime?)DateTime.UtcNow : null;

        var order = new ExchangeOrder
        {
            SignalId        = signal.Id > 0 ? signal.Id : null,
            Exchange        = req.Exchange,
            Symbol          = $"{coin.Symbol?.ToUpperInvariant()}USDT",
            Side            = req.Side,
            Type            = req.OrderType,
            MarketType      = req.MarketType,
            Leverage        = req.Leverage,
            Qty             = qty,
            Entry           = entry,
            StopLoss        = req.StopLossPrice,
            TakeProfit      = req.TakeProfitPrice,
            TakeProfit2     = req.TakeProfit2Price,
            Tp1ClosePct     = req.Tp1ClosePct,
            Tp2ClosePct     = req.Tp2ClosePct,
            Status          = status,
            ExternalOrderId = $"PAPER-{Guid.NewGuid():N}",
            IsPaper         = true,
            CreatedAt       = DateTime.UtcNow,
            FilledAt        = filledAt,
            Notes           = req.Notes,
            WatchedSetupId  = req.WatchedSetupId,
        };

        context.ExchangeOrders.Add(order);
        await context.SaveChangesAsync();

        Logger.Information(
            "TradeService: paper {Type} {MarketType} {Side} {Symbol} qty={Qty:F6} @ {Entry}  " +
            "SL={SL}  TP1={TP}  TP2={TP2}  lev={Lev}×  status={Status}",
            order.Type, order.MarketType, order.Side, order.Symbol, order.Qty, order.Entry,
            order.StopLoss, order.TakeProfit, order.TakeProfit2, order.Leverage, order.Status);

        return order;
    }

    // -----------------------------------------------------------------------
    // Live trading — Sprint 2
    // -----------------------------------------------------------------------

    public Task<ExchangeOrder> PlaceLiveAsync(Coin coin, Signal signal, OrderRequest req)
        => throw new NotImplementedException("Live trading is not available in Sprint 1.4.");

    // -----------------------------------------------------------------------
    // Shared
    // -----------------------------------------------------------------------

    public async Task<bool> CancelAsync(ExchangeOrder order)
    {
        if (order.IsPaper)
        {
            var context = _portfolioService.Context;
            if (context is null) return false;

            order.Status   = OrderStatus.Cancelled;
            order.FilledAt = null;
            context.ExchangeOrders.Update(order);
            await context.SaveChangesAsync();

            Logger.Information("TradeService: paper order #{Id} cancelled", order.Id);
            return true;
        }

        throw new NotImplementedException("Live order cancellation not available in Sprint 1.4.");
    }

    public async Task<bool> ClosePaperAsync(ExchangeOrder order, double closePrice)
    {
        if (!order.IsPaper) throw new InvalidOperationException("ClosePaperAsync is only for paper trades.");

        var context = _portfolioService.Context;
        if (context is null) return false;

        order.Status     = OrderStatus.Closed;
        order.ClosePrice = closePrice;
        order.ClosedAt   = DateTime.UtcNow;
        context.ExchangeOrders.Update(order);
        await context.SaveChangesAsync();

        double pnl = order.Side == Enums.OrderSide.Buy
            ? Math.Round((closePrice - order.Entry) * order.Qty, 2)
            : Math.Round((order.Entry - closePrice) * order.Qty, 2);

        Logger.Information(
            "TradeService: paper #{Id} {Symbol} closed @ {Close:F4}  PnL={Pnl:+0.00;-0.00} USDT",
            order.Id, order.Symbol, closePrice, pnl);
        return true;
    }

    public async Task<int> CloseAllPaperAsync(Dictionary<string, double> priceMap)
    {
        var context = _portfolioService.Context;
        if (context is null) return 0;

        var open = await context.ExchangeOrders
            .Where(o => o.IsPaper && o.Status == OrderStatus.Filled)
            .ToListAsync();

        int closed = 0;
        var now = DateTime.UtcNow;
        foreach (var order in open)
        {
            var baseSymbol = order.Symbol.Replace("USDT", "").ToUpperInvariant();
            if (!priceMap.TryGetValue(baseSymbol, out var price) || price <= 0) continue;

            order.Status     = OrderStatus.Closed;
            order.ClosePrice = price;
            order.ClosedAt   = now;
            closed++;
        }

        if (closed > 0) await context.SaveChangesAsync();

        Logger.Information("TradeService: kill-all closed {N} paper positions", closed);
        return closed;
    }

    public Task SyncFillsAsync() => Task.CompletedTask; // Sprint 2

    // -----------------------------------------------------------------------
    // Automatic fill monitoring (Pending → Filled)
    // -----------------------------------------------------------------------

    public async Task<List<(int OrderId, string Symbol)>> AutoFillPendingAsync(
        Dictionary<string, double> priceMap)
    {
        var context = _portfolioService.Context;
        if (context is null) return new();

        var pending = await context.ExchangeOrders
            .Where(o => o.IsPaper && o.Status == OrderStatus.Pending)
            .ToListAsync();

        var filled = new List<(int, string)>();
        var now = DateTime.UtcNow;

        foreach (var order in pending)
        {
            var baseSymbol = order.Symbol.Replace("USDT", "").ToUpperInvariant();
            if (!priceMap.TryGetValue(baseSymbol, out var price) || price <= 0) continue;
            if (order.Entry <= 0) continue;

            // Buy limit: fills when market price drops to or below the entry level.
            // Sell limit: fills when market price rises to or above the entry level.
            bool shouldFill = order.Side == OrderSide.Buy
                ? price <= order.Entry
                : price >= order.Entry;

            if (!shouldFill) continue;

            order.Status   = OrderStatus.Filled;
            order.FilledAt = now;
            filled.Add((order.Id, order.Symbol));

            Logger.Information(
                "TradeService AUTO-FILL: #{Id} {Symbol} {Side} entry={Entry} current={Price} → Filled",
                order.Id, order.Symbol, order.Side, order.Entry, price);

            if (_notifier is not null)
                await _notifier.SendAlertAsync(
                    $"✅ <b>Entry gevuld</b> — {order.Symbol} {order.Side}\n" +
                    $"Limit {order.Entry:#,0.########} bereikt (koers {price:#,0.########}).");
        }

        if (filled.Count > 0)
            await context.SaveChangesAsync();

        return filled;
    }

    // -----------------------------------------------------------------------
    // Automatic TP / SL monitoring
    // -----------------------------------------------------------------------

    public async Task<List<(int OrderId, string Symbol, string Reason)>> AutoCloseTriggeredAsync(
        Dictionary<string, double> priceMap)
    {
        var context = _portfolioService.Context;
        if (context is null) return new();

        var openOrders = await context.ExchangeOrders
            .Where(o => o.IsPaper && o.Status == OrderStatus.Filled)
            .ToListAsync();

        var closed = new List<(int, string, string)>();

        foreach (var order in openOrders)
        {
            var baseSymbol = order.Symbol.Replace("USDT", "").ToUpperInvariant();
            if (!priceMap.TryGetValue(baseSymbol, out var price) || price <= 0) continue;

            bool isLong = order.Side == OrderSide.Buy;
            string? reason = null;
            double  closeAt = 0;

            // ── Check Stop Loss (priority over TP) ──────────────────────────
            if (order.StopLoss > 0)
            {
                bool slHit = isLong ? price <= order.StopLoss : price >= order.StopLoss;
                if (slHit)
                {
                    reason  = $"🛑 SL geraakt @ {order.StopLoss:#,0.########}";
                    closeAt = order.StopLoss;
                }
            }

            // ── Check TP2 first (if price already passed TP1 as well) ───────
            if (reason is null && order.TakeProfit2 > 0)
            {
                bool tp2Hit = isLong ? price >= order.TakeProfit2 : price <= order.TakeProfit2;
                if (tp2Hit)
                {
                    reason  = $"🎯 TP2 geraakt @ {order.TakeProfit2:#,0.########}";
                    closeAt = order.TakeProfit2;
                }
            }

            // ── Check TP1 ───────────────────────────────────────────────────
            if (reason is null && order.TakeProfit > 0)
            {
                bool tp1Hit = isLong ? price >= order.TakeProfit : price <= order.TakeProfit;
                if (tp1Hit)
                {
                    reason  = $"🎯 TP1 geraakt @ {order.TakeProfit:#,0.########}";
                    closeAt = order.TakeProfit;
                }
            }

            if (reason is null) continue;

            // Close the order at the TP/SL price
            order.Status     = OrderStatus.Closed;
            order.ClosePrice = closeAt;
            order.ClosedAt   = DateTime.UtcNow;
            // Prepend auto-close reason to notes
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? $"[Auto] {reason}"
                : $"[Auto] {reason} | {order.Notes}";

            closed.Add((order.Id, order.Symbol, reason));

            double pnl = order.Side == OrderSide.Buy
                ? Math.Round((closeAt - order.Entry) * order.Qty, 2)
                : Math.Round((order.Entry - closeAt) * order.Qty, 2);

            Logger.Information(
                "TradeService AUTO-CLOSE: #{Id} {Symbol} {Reason} PnL={Pnl:+0.00;-0.00} USDT",
                order.Id, order.Symbol, reason, pnl);

            if (_notifier is not null)
                await _notifier.SendAlertAsync(
                    $"{(pnl >= 0 ? "🎯" : "🛑")} <b>Positie gesloten</b> — {order.Symbol} {order.Side}\n" +
                    $"{reason}\nP&amp;L: <b>{pnl:+0.00;-0.00} USDT</b>");
        }

        if (closed.Count > 0)
        {
            await context.SaveChangesAsync();
            await AlertIfDailyLossLimitCrossedAsync(context);
        }

        return closed;
    }

    /// <summary>
    /// Stuurt (eenmaal per dag) een alert wanneer de gerealiseerde paper-dag-P&amp;L
    /// door de ingestelde dagelijkse verlieslimiet zakt.
    /// </summary>
    private async Task AlertIfDailyLossLimitCrossedAsync(Infrastructure.PortfolioContext context)
    {
        if (_notifier is null || _guardrails is null) return;
        if (_lastDailyLossAlertDay == DateTime.UtcNow.Date) return;   // al gemeld vandaag

        try
        {
            var verdict = await _guardrails.CheckNewTradeAsync();
            var lossReason = verdict.Reasons.FirstOrDefault(r => r.Contains("verlieslimiet"));
            if (lossReason is null) return;

            _lastDailyLossAlertDay = DateTime.UtcNow.Date;
            await _notifier.SendAlertAsync(
                $"🚨 <b>Dagelijkse verlieslimiet bereikt</b>\n{lossReason}\n" +
                "Nieuwe paper trades zijn geblokkeerd voor de rest van de dag.");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TradeService: dagverlies-alert mislukt");
        }
    }

    public async Task UpdateNotesAsync(int orderId, string notes)
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        var order = await context.ExchangeOrders.FindAsync(orderId);
        if (order is null) return;

        order.Notes = notes ?? string.Empty;
        await context.SaveChangesAsync();
        Logger.Information("Order #{Id} notes updated", orderId);
    }

    public async Task UpdateOrderLevelsAsync(int orderId, double stopLoss, double takeProfit, double takeProfit2, double currentPrice = 0)
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        var order = await context.ExchangeOrders.FindAsync(orderId);
        if (order is null) return;

        // Een reeds gevulde (open) positie valideren we t.o.v. de HUIDIGE koers: zo mag de stop
        // naar winst worden getrokken (bv. een short-stop onder de entry). Een nog niet gevulde
        // (Pending) order blijft t.o.v. de geplande entry gevalideerd (setup-modus).
        var validation = (order.Status == OrderStatus.Filled && currentPrice > 0)
            ? TradeSetupValidator.ValidateForOpenPosition(order.Side, currentPrice, stopLoss, takeProfit, takeProfit2)
            : TradeSetupValidator.Validate(order.Side, order.Entry, stopLoss, takeProfit, takeProfit2);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Error);

        order.StopLoss    = stopLoss;
        order.TakeProfit  = takeProfit;
        order.TakeProfit2 = takeProfit2;
        await context.SaveChangesAsync();

        Logger.Information(
            "TradeService: order #{Id} levels updated — SL={SL}  TP1={TP1}  TP2={TP2}",
            orderId, stopLoss, takeProfit, takeProfit2);
    }
}
