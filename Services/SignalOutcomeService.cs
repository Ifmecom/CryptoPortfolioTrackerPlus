using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// EF-backed signal-outcome-tracker (v1.46). Dunne lijm rond de pure <see cref="SignalOutcomeEvaluator"/>
/// en <see cref="SignalCalibrationCalculator"/>:
/// <list type="number">
///   <item>Neemt SignalEngine-signalen op uit de bestaande <c>Signals</c>-tabel (ook met terugwerkende kracht).</item>
///   <item>Legt Pattern Trading-scans vast (aangeroepen door <see cref="PatternTradingService"/> ná de scan).</item>
///   <item>Haalt per coin één keer daily klines op (Binance → KuCoin → Gate.io → MEXC) en meet alle
///         openstaande uitkomsten.</item>
/// </list>
/// Gebruikt de gedeelde <see cref="PortfolioService.Context"/>; een interne semafoor serialiseert de
/// DB-toegang. Koersdata wordt buiten de semafoor (parallel, max 3) opgehaald.
/// </summary>
public class SignalOutcomeService : ISignalOutcomeService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(SignalOutcomeService).PadRight(22));

    private static readonly SemaphoreSlim _dbGate    = new(1, 1);
    private static readonly SemaphoreSlim _fetchGate = new(3, 3);   // exchange rate-limits
    private static readonly SemaphoreSlim _runGate   = new(1, 1);   // één bijwerk-ronde tegelijk

    /// <summary>Binance levert max. 1000 daily candles per request.</summary>
    private const int MaxBinanceBars  = 1000;
    /// <summary>De fallback-exchanges worden elders ook met 200 aangeroepen — veilig maximum.</summary>
    private const int MaxFallbackBars = 200;

    private readonly PortfolioService      _portfolio;
    private readonly IBinanceDataService   _binance;
    private readonly IKuCoinDataService    _kuCoin;
    private readonly IGateIoDataService    _gateIo;
    private readonly IMexcDataService      _mexc;
    private readonly IMarketRegimeService? _regime;

    public SignalOutcomeService(
        PortfolioService      portfolio,
        IBinanceDataService   binance,
        IKuCoinDataService    kuCoin,
        IGateIoDataService    gateIo,
        IMexcDataService      mexc,
        IMarketRegimeService? regime = null)
    {
        _portfolio = portfolio;
        _binance   = binance;
        _kuCoin    = kuCoin;
        _gateIo    = gateIo;
        _mexc      = mexc;
        _regime    = regime;
    }

    // =========================================================================
    // Bijwerken: signalen opnemen + openstaande uitkomsten meten
    // =========================================================================

    public async Task<SignalOutcomeUpdateResult> UpdateAsync(
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null) return SignalOutcomeUpdateResult.Empty;

        // Voorkom dat twee schermen tegelijk een ronde starten (dubbele inserts/fetches).
        if (!await _runGate.WaitAsync(0, ct))
            return SignalOutcomeUpdateResult.Empty;

        try
        {
            progress?.Report("Nieuwe signalen opnemen…");
            int added = await SyncSignalsAsync(ctx, ct);

            var nowUtc = DateTime.UtcNow;
            List<SignalOutcome> due;
            await _dbGate.WaitAsync(ct);
            try
            {
                // Filter in het geheugen: de tabel is klein (één rij per coin per dag per bron).
                due = (await ctx.SignalOutcomes.Where(o => !o.IsComplete).ToListAsync(ct))
                      .Where(o => SignalOutcomeEvaluator.IsDue(o, nowUtc))
                      .ToList();
            }
            finally { _dbGate.Release(); }

            if (due.Count == 0)
                return new SignalOutcomeUpdateResult(added, 0, 0, 0, 0);

            // ── Koersdata ophalen: één request per coin, parallel (max 3) ───────
            var byCoin = due.GroupBy(o => o.CoinApiId, StringComparer.OrdinalIgnoreCase).ToList();
            int done = 0;
            var fetches = byCoin.Select(async g =>
            {
                var first  = g.First();
                var oldest = g.Min(o => o.SignalDay).Date;
                int limit  = (int)Math.Ceiling((nowUtc.Date - oldest).TotalDays) + 3;

                await _fetchGate.WaitAsync(ct);
                try
                {
                    var bars = await FetchDailyBarsAsync(first.CoinApiId, first.CoinSymbol, limit);
                    progress?.Report($"Koersdata ophalen… {Interlocked.Increment(ref done)}/{byCoin.Count}");
                    return (Group: g, Bars: bars);
                }
                finally { _fetchGate.Release(); }
            });
            var fetched = await Task.WhenAll(fetches);

            // ── Meten + opslaan (sequentieel, gedeelde context) ─────────────────
            int measured = 0, completed = 0, withoutData = 0;
            await _dbGate.WaitAsync(ct);
            try
            {
                foreach (var (group, bars) in fetched)
                {
                    foreach (var o in group)
                    {
                        var m = SignalOutcomeEvaluator.Measure(o.Direction, o.SignalDay, bars, nowUtc);
                        if (m is null)
                        {
                            o.FailedAttempts++;
                            o.EvaluatedAt = nowUtc;
                            withoutData++;
                            continue;
                        }

                        SignalOutcomeEvaluator.Apply(o, m, nowUtc);
                        measured++;
                        if (m.IsComplete) completed++;
                    }
                }
                await ctx.SaveChangesAsync(ct);
            }
            finally { _dbGate.Release(); }

            int pending = due.Count(o => !o.IsComplete);
            Logger.Information(
                "SignalOutcomes: {Added} nieuw, {Measured} gemeten ({Completed} compleet), {NoData} zonder data, {Pending} open",
                added, measured, completed, withoutData, pending);

            return new SignalOutcomeUpdateResult(added, measured, completed, withoutData, pending);
        }
        catch (OperationCanceledException)
        {
            return SignalOutcomeUpdateResult.Empty;
        }
        catch (Exception ex)
        {
            // De tracker mag nooit een scherm breken — log en ga door.
            Logger.Warning(ex, "SignalOutcomes: bijwerken mislukt");
            return SignalOutcomeUpdateResult.Empty;
        }
        finally
        {
            _runGate.Release();
        }
    }

    /// <summary>Neemt nog niet gevolgde SignalEngine-signalen op. Retourneert het aantal nieuwe rijen.</summary>
    private static async Task<int> SyncSignalsAsync(Infrastructure.PortfolioContext ctx, CancellationToken ct)
    {
        await _dbGate.WaitAsync(ct);
        try
        {
            // Bestaande sleutels (coin + dag) voor de Signal-bron.
            var existing = (await ctx.SignalOutcomes.AsNoTracking()
                    .Where(o => o.Source == SignalOutcomeSources.Signal)
                    .Select(o => new { o.CoinApiId, o.SignalDay })
                    .ToListAsync(ct))
                .Select(k => Key(k.CoinApiId, k.SignalDay))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Alleen de velden die we nodig hebben (Reasoning kan groot zijn).
            var rows = await ctx.Signals.AsNoTracking()
                .Select(s => new
                {
                    s.Id, s.CoinId, s.Direction, s.CombinedScore, s.MarketRegimeMultiplier, s.CreatedAt,
                    ApiId  = s.Coin.ApiId,
                    Symbol = s.Coin.Symbol,
                })
                .ToListAsync(ct);

            var coinInfo = rows
                .GroupBy(r => r.CoinId)
                .ToDictionary(g => g.Key, g => (ApiId: g.First().ApiId, Symbol: g.First().Symbol));

            var candidates = rows.Select(r => new Signal
            {
                Id = r.Id, CoinId = r.CoinId, Direction = r.Direction, CombinedScore = r.CombinedScore,
                MarketRegimeMultiplier = r.MarketRegimeMultiplier, CreatedAt = r.CreatedAt,
            });

            int added = 0;
            foreach (var s in SignalOutcomeEvaluator.FirstPerCoinPerDay(candidates))
            {
                var (apiId, symbol) = coinInfo[s.CoinId];
                if (string.IsNullOrWhiteSpace(apiId)) continue;
                if (!existing.Add(Key(apiId, s.CreatedAt.Date))) continue;

                ctx.SignalOutcomes.Add(SignalOutcomeEvaluator.FromSignal(s, apiId, symbol ?? string.Empty));
                added++;
            }

            if (added > 0)
                await ctx.SaveChangesAsync(ct);

            return added;
        }
        finally { _dbGate.Release(); }
    }

    // =========================================================================
    // Pattern Trading-scans vastleggen
    // =========================================================================

    public async Task RecordPatternScanAsync(IEnumerable<PatternCoinAnalysis> results, CancellationToken ct = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null || results is null) return;

        try
        {
            var nowUtc = DateTime.UtcNow;
            string regime = await GetRegimeSafeAsync(ct);

            var candidates = results
                .Where(r => r?.Coin is not null && r.HasData)
                .Select(r => SignalOutcomeEvaluator.FromPatternScan(
                    r.Coin.ApiId, r.Coin.Symbol ?? string.Empty, r.PrimaryDirection,
                    r.TradabilityScore, regime, nowUtc))
                .Where(o => o is not null)
                .Select(o => o!)
                .ToList();

            if (candidates.Count == 0) return;

            await _dbGate.WaitAsync(ct);
            try
            {
                var today = nowUtc.Date;
                var existingToday = (await ctx.SignalOutcomes.AsNoTracking()
                        .Where(o => o.Source == SignalOutcomeSources.Pattern && o.SignalDay == today)
                        .Select(o => o.CoinApiId)
                        .ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                int added = 0;
                foreach (var o in candidates)
                {
                    // Eerste scan van de dag telt (zelfde logica als bij de SignalEngine).
                    if (!existingToday.Add(o.CoinApiId)) continue;
                    ctx.SignalOutcomes.Add(o);
                    added++;
                }

                if (added > 0)
                {
                    await ctx.SaveChangesAsync(ct);
                    Logger.Information("SignalOutcomes: {Count} Pattern Trading-signalen vastgelegd", added);
                }
            }
            finally { _dbGate.Release(); }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SignalOutcomes: vastleggen Pattern-scan mislukt");
        }
    }

    // =========================================================================
    // Lezen
    // =========================================================================

    public async Task<IReadOnlyList<SignalOutcome>> GetAllAsync(CancellationToken ct = default)
    {
        var ctx = _portfolio.Context;
        if (ctx is null) return Array.Empty<SignalOutcome>();

        await _dbGate.WaitAsync(ct);
        try
        {
            return await ctx.SignalOutcomes.AsNoTracking()
                .OrderByDescending(o => o.SignalAt)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SignalOutcomes: lezen mislukt");
            return Array.Empty<SignalOutcome>();
        }
        finally { _dbGate.Release(); }
    }

    public async Task<IReadOnlyList<SignalCalibrationRow>> GetCalibrationAsync(
        string source, int horizonDays = SignalCalibrationCalculator.DefaultHorizonDays, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return SignalCalibrationCalculator.Compute(all, source, horizonDays);
    }

    // =========================================================================
    // Hulpfuncties
    // =========================================================================

    private static string Key(string apiId, DateTime day) => $"{apiId}|{day:yyyy-MM-dd}";

    private async Task<string> GetRegimeSafeAsync(CancellationToken ct)
    {
        if (_regime is null) return string.Empty;
        try
        {
            var ctxRegime = await _regime.GetRegimeContextAsync(ct);
            return ctxRegime.Regime.ToString();
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "SignalOutcomes: regime niet beschikbaar");
            return string.Empty;
        }
    }

    /// <summary>Daily klines via dezelfde keten als Pattern Trading: Binance → KuCoin → Gate.io → MEXC.</summary>
    private async Task<List<OhlcvBar>> FetchDailyBarsAsync(string coinApiId, string coinSymbol, int limit)
    {
        limit = Math.Max(limit, 20);
        try
        {
            var bars = await _binance.GetKlinesAsync(
                _binance.ResolveBinanceSymbol(coinApiId, coinSymbol), "1d", Math.Min(limit, MaxBinanceBars));
            if (bars.Count > 0) return bars;

            if (string.IsNullOrWhiteSpace(coinSymbol)) return new();
            int fb = Math.Min(limit, MaxFallbackBars);

            bars = await _kuCoin.GetKlinesAsync(_kuCoin.ResolveKuCoinSymbol(coinSymbol), "1d", fb);
            if (bars.Count > 0) return bars;

            bars = await _gateIo.GetKlinesAsync(_gateIo.ResolveSymbol(coinSymbol), "1d", fb);
            if (bars.Count > 0) return bars;

            return await _mexc.GetKlinesAsync(_mexc.ResolveSymbol(coinSymbol), "1d", fb);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "SignalOutcomes: geen koersdata voor {Coin}", coinApiId);
            return new();
        }
    }
}
