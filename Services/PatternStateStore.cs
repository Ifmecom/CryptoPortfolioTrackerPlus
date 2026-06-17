using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// EF-backed implementatie van <see cref="IPatternStateStore"/>. Dunne lijm rond de pure
/// <see cref="PatternReconciler"/>: laadt de actieve records van de coin, verzoent, persisteert en
/// verrijkt de detecties. Gebruikt de gedeelde <see cref="PortfolioService.Context"/>; een interne
/// semafoor serialiseert DB-toegang zodat aanroepen elkaar niet overlappen (de patroon-scan draait
/// per-coin parallel, maar deze reconciliatie wordt sequentieel ná de scan aangeroepen).
/// </summary>
public class PatternStateStore : IPatternStateStore
{
    private readonly PortfolioService _portfolio;
    private readonly ILogger _log;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public PatternStateStore(PortfolioService portfolio)
    {
        _portfolio = portfolio;
        _log = Log.Logger.ForContext(
            Constants.SourceContextPropertyName, nameof(PatternStateStore).PadRight(22));
    }

    public async Task<IReadOnlyList<PatternTransition>> ReconcileCoinAsync(
        string coinApiId,
        string coinSymbol,
        IReadOnlyList<PatternResult> detections,
        double currentPrice,
        CancellationToken ct = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null || string.IsNullOrWhiteSpace(coinApiId))
            return Array.Empty<PatternTransition>();

        // Alleen patronen met een levenscyclus: geometrische/niveau-patronen. Level-1 indicator-
        // signalen (RSI/MACD/EMA/squeeze) zijn momentaan en hebben geen geheugen.
        var relevant = detections
            .Where(p => p.KeyLevel.HasValue || p.Annotation is not null)
            .ToList();

        await _gate.WaitAsync(ct);
        try
        {
            var existing = await ctx.PatternStates
                .Where(p => p.CoinApiId == coinApiId && p.IsActive)
                .ToListAsync(ct);

            var res = PatternReconciler.Reconcile(
                coinApiId, coinSymbol, existing, relevant, currentPrice, DateTime.UtcNow);

            foreach (var rec in res.Upserts)
                if (rec.Id == 0) ctx.PatternStates.Add(rec);

            await ctx.SaveChangesAsync(ct);

            // Verrijk de oorspronkelijke detecties met de gepersisteerde levenscyclus.
            foreach (var det in relevant)
            {
                string fp  = PatternFingerprint.For(coinApiId, det);
                double key = det.KeyLevel ?? 0;
                var rec = res.Upserts.FirstOrDefault(r =>
                    r.IsActive && r.Fingerprint == fp && PatternFingerprint.LevelMatches(key, r.KeyLevel));
                if (rec is null) continue;
                det.Lifecycle       = rec.Lifecycle;
                det.TimesSeen       = rec.TimesSeen;
                det.LifecycleReason = rec.LastTransitionReason;
            }

            return res.Transitions;
        }
        catch (Exception ex)
        {
            // Geheugen mag de scan nooit breken — log en ga door.
            _log.Warning(ex, "PatternStateStore reconcile mislukt voor {Coin}", coinApiId);
            return Array.Empty<PatternTransition>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PatternHistoryStat>> GetHistoryStatsAsync(CancellationToken ct = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null) return Array.Empty<PatternHistoryStat>();

        await _gate.WaitAsync(ct);
        try
        {
            var records = await ctx.PatternStates.AsNoTracking().ToListAsync(ct);
            return PatternHistoryCalculator.Compute(records);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PatternStateStore historie-statistiek mislukt");
            return Array.Empty<PatternHistoryStat>();
        }
        finally
        {
            _gate.Release();
        }
    }
}
