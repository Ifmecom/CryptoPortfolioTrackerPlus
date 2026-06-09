using System;
using System.Collections.Generic;
using System.Linq;
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
/// Verzamelt de open posities + gerealiseerde dag-P&amp;L uit de ExchangeOrders en bouwt via de pure
/// <see cref="RiskDashboardCalculator"/> het risico-overzicht. Rekent tegen het virtuele paper-kapitaal,
/// consistent met de PaperTradeDialog en de positiegrootte-berekening.
/// </summary>
public class RiskDashboardService : IRiskDashboardService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(RiskDashboardService).PadRight(22));

    private const double Capital = 10_000.0;   // virtueel paper-kapitaal (zoals PaperTradeDialog)

    private readonly PortfolioService _portfolioService;
    private readonly Settings         _settings;

    public RiskDashboardService(PortfolioService portfolioService, Settings settings)
    {
        _portfolioService = portfolioService;
        _settings         = settings;
    }

    public async Task<RiskDashboard> BuildAsync(CancellationToken ct = default)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null)
            return RiskDashboardCalculator.Build(Array.Empty<RiskPosition>(), 0, Capital,
                _settings.MaxOpenPositions, _settings.DailyLossLimitPerc,
                _settings.MaxPortfolioPercPerTrade, _settings.IsKillSwitchActive);

        // Open posities (gevuld, nog niet gesloten)
        var open = await ctx.ExchangeOrders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Filled)
            .ToListAsync(ct);

        // Live koersen per basis-symbool
        var coins = await ctx.Coins.AsNoTracking()
            .Where(c => c.Symbol != "")
            .Select(c => new { c.Symbol, c.Price })
            .ToListAsync(ct);
        var priceMap = coins
            .GroupBy(c => c.Symbol!.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Price, StringComparer.OrdinalIgnoreCase);

        var positions = open.Select(o =>
        {
            var baseSym = o.Symbol.Replace("USDT", "").ToUpperInvariant();
            priceMap.TryGetValue(baseSym, out double price);
            return new RiskPosition(baseSym, o.Entry, o.StopLoss, o.Qty, price);
        }).ToList();

        // Gerealiseerde P&L van vandaag (UTC-dag)
        var todayUtc = DateTime.UtcNow.Date;
        var closedToday = await ctx.ExchangeOrders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Closed && o.ClosedAt != null && o.ClosedAt >= todayUtc)
            .ToListAsync(ct);

        double dayPnl = closedToday
            .Where(o => o.ClosePrice > 0 && o.Entry > 0)
            .Sum(o => o.Side == OrderSide.Buy
                ? (o.ClosePrice - o.Entry) * o.Qty
                : (o.Entry - o.ClosePrice) * o.Qty);

        return RiskDashboardCalculator.Build(
            positions, dayPnl, Capital,
            _settings.MaxOpenPositions, _settings.DailyLossLimitPerc,
            _settings.MaxPortfolioPercPerTrade, _settings.IsKillSwitchActive);
    }
}
