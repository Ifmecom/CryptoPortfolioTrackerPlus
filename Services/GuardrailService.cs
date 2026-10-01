using System;
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
/// Handhaaft de risk-guardrails bij het plaatsen van nieuwe paper trades:
/// telt de open paper-posities en de gerealiseerde paper-dag-P&amp;L en evalueert die
/// via de pure <see cref="GuardrailEvaluator"/> tegen de instellingen.
/// De verlieslimiet rekent tegen de gekozen kapitaalbasis (<see cref="IRiskCapitalService"/>).
/// </summary>
public class GuardrailService : IGuardrailService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(GuardrailService).PadRight(22));

    private readonly PortfolioService    _portfolioService;
    private readonly Settings            _settings;
    private readonly IRiskCapitalService _capital;

    public GuardrailService(PortfolioService portfolioService, Settings settings, IRiskCapitalService capital)
    {
        _portfolioService = portfolioService;
        _settings         = settings;
        _capital          = capital;
    }

    public Task<GuardrailVerdict> CheckNewTradeAsync(CancellationToken ct = default)
        => CheckAsync(paper: true, exchange: null, ct);

    /// <summary>Guardrails voor een live/demo-order: alleen posities en P&amp;L van die exchange tellen mee (v1.47).</summary>
    public Task<GuardrailVerdict> CheckNewLiveTradeAsync(ExchangeKind exchange, CancellationToken ct = default)
        => CheckAsync(paper: false, exchange: exchange, ct);

    private async Task<GuardrailVerdict> CheckAsync(bool paper, ExchangeKind? exchange, CancellationToken ct)
    {
        try
        {
            var ctx = _portfolioService.Context;
            if (ctx is null) return GuardrailVerdict.Allowed;   // geen DB → niet blokkeren

            var scope = ctx.ExchangeOrders.AsNoTracking().Where(o => o.IsPaper == paper);
            if (exchange is { } ex)
                scope = scope.Where(o => o.Exchange == ex);

            // Live: ook een nog niet gevulde order telt als (bijna) open positie.
            int openCount = paper
                ? await scope.CountAsync(o => o.Status == OrderStatus.Filled, ct)
                : await scope.CountAsync(o => o.Status == OrderStatus.Filled
                                           || o.Status == OrderStatus.Pending
                                           || o.Status == OrderStatus.PartiallyFilled, ct);

            var todayUtc = DateTime.UtcNow.Date;
            var closedToday = await scope
                .Where(o => o.Status == OrderStatus.Closed && o.ClosedAt != null && o.ClosedAt >= todayUtc)
                .ToListAsync(ct);

            double dayPnl = closedToday
                .Where(o => o.ClosePrice > 0 && o.Entry > 0)
                .Sum(o => o.Side == OrderSide.Buy
                    ? (o.ClosePrice - o.Entry) * o.Qty
                    : (o.Entry - o.ClosePrice) * o.Qty);

            double capital  = await _capital.GetCapitalAsync(ct);
            double limitUsd = _settings.DailyLossLimitPerc > 0
                ? capital * _settings.DailyLossLimitPerc / 100.0 : 0;

            var verdict = GuardrailEvaluator.Evaluate(
                _settings.IsKillSwitchActive, openCount, _settings.MaxOpenPositions, dayPnl, limitUsd);

            if (verdict.IsBlocked)
                Logger.Information("Guardrails blokkeren nieuwe {Scope} trade: {Reasons}",
                    paper ? "paper" : exchange?.ToString() ?? "live", verdict.ReasonText);
            return verdict;
        }
        catch (Exception ex)
        {
            // Een falende check mag het handelen niet stilleggen — log en sta toe.
            Logger.Warning(ex, "GuardrailService: check mislukt — trade toegestaan");
            return GuardrailVerdict.Allowed;
        }
    }
}
