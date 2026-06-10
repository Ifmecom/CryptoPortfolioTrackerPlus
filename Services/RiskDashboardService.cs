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
/// Verzamelt de open posities + gerealiseerde dag-P&amp;L uit de ExchangeOrders (gescheiden naar
/// paper of live) en bouwt via de pure <see cref="RiskDashboardCalculator"/> het risico-overzicht.
/// Paper rekent tegen de gekozen kapitaalbasis (instelling); live altijd tegen de echte portfoliowaarde.
/// </summary>
public class RiskDashboardService : IRiskDashboardService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(RiskDashboardService).PadRight(22));

    private readonly PortfolioService    _portfolioService;
    private readonly Settings            _settings;
    private readonly IRiskCapitalService _capital;

    public RiskDashboardService(PortfolioService portfolioService, Settings settings, IRiskCapitalService capital)
    {
        _portfolioService = portfolioService;
        _settings         = settings;
        _capital          = capital;
    }

    public async Task<RiskDashboard> BuildAsync(RiskScope scope = RiskScope.Paper, CancellationToken ct = default)
    {
        bool paper = scope == RiskScope.Paper;

        // Paper volgt de gekozen kapitaalbasis; live rekent altijd tegen de echte portfoliowaarde.
        double capital;
        string basis;
        if (paper)
        {
            capital = await _capital.GetCapitalAsync(ct);
            basis   = _capital.BasisLabel;
        }
        else
        {
            capital = await _capital.GetRealPortfolioValueAsync(ct);
            basis   = "echte portfoliowaarde";
        }

        var ctx = _portfolioService.Context;
        if (ctx is null)
            return RiskDashboardCalculator.Build(Array.Empty<RiskPosition>(), 0, capital,
                _settings.MaxOpenPositions, _settings.DailyLossLimitPerc,
                _settings.MaxPortfolioPercPerTrade, _settings.IsKillSwitchActive, basis);

        // Open posities (gevuld, nog niet gesloten) — gescheiden naar paper of live
        var open = await ctx.ExchangeOrders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Filled && o.IsPaper == paper)
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
            .Where(o => o.Status == OrderStatus.Closed && o.IsPaper == paper
                     && o.ClosedAt != null && o.ClosedAt >= todayUtc)
            .ToListAsync(ct);

        double dayPnl = closedToday
            .Where(o => o.ClosePrice > 0 && o.Entry > 0)
            .Sum(o => o.Side == OrderSide.Buy
                ? (o.ClosePrice - o.Entry) * o.Qty
                : (o.Entry - o.ClosePrice) * o.Qty);

        return RiskDashboardCalculator.Build(
            positions, dayPnl, capital,
            _settings.MaxOpenPositions, _settings.DailyLossLimitPerc,
            _settings.MaxPortfolioPercPerTrade, _settings.IsKillSwitchActive, basis);
    }
}
