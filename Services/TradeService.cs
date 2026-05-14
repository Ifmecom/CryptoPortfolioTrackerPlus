using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class TradeService : ITradeService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(TradeService).PadRight(22));

    private readonly PortfolioService _portfolioService;

    public TradeService(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
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

        var entry = coin.Price;
        var qty   = req.AmountUsdt / entry;

        var sl = req.Side == OrderSide.Buy
            ? Math.Round(entry * (1.0 - req.StopLossPerc   / 100.0), 8)
            : Math.Round(entry * (1.0 + req.StopLossPerc   / 100.0), 8);

        var tp = req.Side == OrderSide.Buy
            ? Math.Round(entry * (1.0 + req.TakeProfitPerc / 100.0), 8)
            : Math.Round(entry * (1.0 - req.TakeProfitPerc / 100.0), 8);

        var order = new ExchangeOrder
        {
            SignalId        = signal.Id > 0 ? signal.Id : null,
            Exchange        = req.Exchange,
            Symbol          = $"{coin.Symbol?.ToUpperInvariant()}USDT",
            Side            = req.Side,
            Type            = OrderType.Market,
            Qty             = Math.Round(qty, 8),
            Entry           = entry,
            StopLoss        = sl,
            TakeProfit      = tp,
            Status          = OrderStatus.Filled,          // instant fill for paper
            ExternalOrderId = $"PAPER-{Guid.NewGuid():N}",
            IsPaper         = true,
            CreatedAt       = DateTime.UtcNow,
            FilledAt        = DateTime.UtcNow,
        };

        context.ExchangeOrders.Add(order);
        await context.SaveChangesAsync();

        Logger.Information(
            "TradeService: paper {Side} {Symbol} qty={Qty:F6} @ {Entry:F4}  SL={SL:F4}  TP={TP:F4}",
            order.Side, order.Symbol, order.Qty, order.Entry, order.StopLoss, order.TakeProfit);

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
}
