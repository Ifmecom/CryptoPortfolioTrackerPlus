using System.Text.Json;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Backtest-engine voor de 3%-trading-strategie.
///
/// Algoritme per bar i (vanaf bar 210 om EMA200 te kunnen berekenen):
///   1. Score de coin met bars[0..i].
///   2. Bereken structureel SL en bruto TP.
///   3. Scan bars[i+1..i+MaxHorizon]: raakt High >= TP (win) of Low <= SL (loss)?
///   4. Timeout (geen positie geteld) als noch TP noch SL geraakt wordt.
///
/// Kalibratie-resultaten worden opgeslagen als JSON in AppDataPath.
/// </summary>
public class ThreePctBacktestService : IThreePctBacktestService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(ThreePctBacktestService).PadRight(22));

    private const string CalibrationFileName = "3pct_calibration.json";
    private const int    MinTradesReliable   = 30;

    private readonly IBinanceDataService   _binance;
    private readonly IThreePctScoringService _scorer;

    public ThreePctBacktestService(IBinanceDataService binance, IThreePctScoringService scorer)
    {
        _binance = binance;
        _scorer  = scorer;
    }

    // =========================================================================
    // RunAsync
    // =========================================================================

    public async Task<List<ScoreClassCalibration>> RunAsync(
        string             binanceSymbol,
        BacktestParameters pars,
        IProgress<(int done, int total, string status)> progress,
        CancellationToken  ct = default)
    {
        progress.Report((0, 0, $"Data ophalen voor {binanceSymbol} ({pars.Timeframe})…"));

        // Fetch max 1000 bars (Binance limit); for 1D that's ~2.75 years
        var bars = await _binance.GetKlinesAsync(binanceSymbol, pars.Timeframe, limit: 1000);

        if (bars.Count < 220)
        {
            Logger.Warning("ThreePctBacktest: insufficient bars ({N}) for {Symbol}", bars.Count, binanceSymbol);
            return new List<ScoreClassCalibration>();
        }

        progress.Report((0, 0, $"{bars.Count} candles geladen — simulatie starten…"));

        // ── Simulate ─────────────────────────────────────────────────────────
        var trades = new List<(string scoreClass, bool win, double winR)>();
        int start  = 210;  // need 210 bars before for EMA200

        for (int i = start; i < bars.Count - 1; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i % 50 == 0)
                progress.Report((i - start, bars.Count - start, $"Bar {i}/{bars.Count}…"));

            // Score on bars[0..i] (inclusive), current candle = bars[i]
            var window = bars.Take(i + 1).ToList();
            var result = _scorer.Score(window, binanceSymbol, binanceSymbol, pars.Bias, pars);
            if (result is null) continue;

            string scoreClass = IThreePctScoringService.GetScoreClass(result.TotalScore);

            double entry  = result.EntryPrice;
            double sl     = result.StopLoss;
            double tp     = result.TakeProfit;
            double winR   = result.RiskReward;   // R won if TP hit
            bool   isLong = pars.Bias != "Short";

            // Forward scan
            bool win = false, loss = false;
            for (int j = i + 1; j <= i + pars.MaxHorizonBars && j < bars.Count; j++)
            {
                var fwd = bars[j];
                if (isLong)
                {
                    if (fwd.High >= tp) { win  = true; break; }
                    if (fwd.Low  <= sl) { loss = true; break; }
                }
                else
                {
                    if (fwd.Low  <= tp) { win  = true; break; }
                    if (fwd.High >= sl) { loss = true; break; }
                }
            }

            // Timeout: don't count (trade never triggered a clear outcome)
            if (!win && !loss) continue;

            trades.Add((scoreClass, win, win ? winR : 0));
        }

        progress.Report((bars.Count - start, bars.Count - start, "Statistieken berekenen…"));

        // ── Aggregate per score class ─────────────────────────────────────────
        var allClasses = new[] { "0-40", "41-60", "61-80", "81-100" };
        var result2    = new List<ScoreClassCalibration>();

        foreach (var cls in allClasses)
        {
            var group  = trades.Where(t => t.scoreClass == cls).ToList();
            int wins   = group.Count(t => t.win);
            int losses = group.Count - wins;
            int total  = group.Count;

            double hitrate    = total > 0 ? 100.0 * wins / total : 0;
            double avgWinR    = wins   > 0 ? group.Where(t => t.win).Average(t => t.winR) : 0;
            double expectancy = total  > 0
                ? (hitrate / 100.0) * avgWinR - (1.0 - hitrate / 100.0) * 1.0
                : 0;
            double avgR = total > 0
                ? (wins * avgWinR + losses * -1.0) / total
                : 0;

            result2.Add(new ScoreClassCalibration
            {
                ScoreClass   = cls,
                TradeCount   = total,
                HitratePct   = Math.Round(hitrate, 1),
                AvgRMultiple = Math.Round(avgR, 3),
                Expectancy   = Math.Round(expectancy, 3),
                Timeframe    = pars.Timeframe,
                Bias         = pars.Bias,
                CalibratedAt = DateTime.UtcNow,
                IsReliable   = total >= MinTradesReliable,
            });

            Logger.Information(
                "ThreePctBacktest [{Cls}]: {N} trades, hitrate={HR:F1}%, E={E:+0.000;-0.000}R",
                cls, total, hitrate, expectancy);
        }

        SaveCalibration(result2);
        progress.Report((bars.Count - start, bars.Count - start, "Kalibratie opgeslagen ✓"));
        return result2;
    }

    // =========================================================================
    // Persistence
    // =========================================================================

    public List<ScoreClassCalibration>? LoadCalibration()
    {
        try
        {
            var path = CalibrationPath();
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<ScoreClassCalibration>>(json);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "ThreePctBacktest: could not load calibration");
            return null;
        }
    }

    public void SaveCalibration(List<ScoreClassCalibration> results)
    {
        try
        {
            var path = CalibrationPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "ThreePctBacktest: could not save calibration");
        }
    }

    private static string CalibrationPath() =>
        Path.Combine(AppConstants.AppDataPath, CalibrationFileName);
}
