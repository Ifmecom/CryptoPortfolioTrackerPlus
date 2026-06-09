using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare kalibratie: berekent de werkelijk behaalde win-rate en expectancy
/// per scoreklasse uit gesloten <see cref="WatchedSetup"/>s. Dit is de empirische
/// tegenhanger van de 3%-Trading-backtest — gebaseerd op échte uitkomsten i.p.v. simulatie.
/// </summary>
public static class SetupOutcomeCalibrator
{
    /// <summary>Minimum aantal gesloten trades voor een betrouwbare uitspraak per klasse.</summary>
    public const int MinReliable = 10;

    /// <summary>Vaste scoreklassen — consistent met de 3%-Trading-kalibratie.</summary>
    public static readonly string[] Buckets = { "0-40", "41-60", "61-80", "81-100" };

    public static string Bucket(int score) => score switch
    {
        <= 40 => "0-40",
        <= 60 => "41-60",
        <= 80 => "61-80",
        _     => "81-100",
    };

    /// <summary>Gerealiseerde R-multiple van een gesloten setup: reward / risk. 0 als niet te bepalen.</summary>
    public static double RMultiple(WatchedSetup s)
    {
        double risk = Math.Abs(s.EntryPrice - s.StopLoss);
        if (risk <= 0 || s.EntryPrice <= 0 || s.ClosePrice is not { } close) return 0;

        double reward = s.Direction == "Short"
            ? s.EntryPrice - close
            : close - s.EntryPrice;
        return reward / risk;
    }

    /// <summary>
    /// Kalibreert over alle gesloten (Won/Lost) setups en geeft per scoreklasse de werkelijke
    /// win-rate en gemiddelde R. Lege klassen worden ook teruggegeven (TradeCount 0) zodat de
    /// UI een volledige tabel kan tonen.
    /// </summary>
    public static List<ScoreBucketCalibration> Calibrate(IEnumerable<WatchedSetup> setups)
    {
        var closed = setups
            .Where(s => s.Status is WatchedSetupStatus.Won or WatchedSetupStatus.Lost)
            .ToList();

        var byBucket = closed.GroupBy(s => Bucket(s.Score))
                             .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ScoreBucketCalibration>();
        foreach (var bucket in Buckets)
        {
            if (!byBucket.TryGetValue(bucket, out var trades) || trades.Count == 0)
            {
                result.Add(new ScoreBucketCalibration(bucket, 0, 0, 0, 0, 0, false));
                continue;
            }

            int won  = trades.Count(t => t.Status == WatchedSetupStatus.Won);
            int lost  = trades.Count(t => t.Status == WatchedSetupStatus.Lost);
            int total = won + lost;

            double winRate = total > 0 ? 100.0 * won / total : 0;

            // Expectancy = gemiddelde R over trades waar R betekenisvol te bepalen is.
            var rValues = trades.Select(RMultiple).Where(r => r != 0).ToList();
            double expectancy = rValues.Count > 0 ? rValues.Average() : 0;

            result.Add(new ScoreBucketCalibration(
                bucket, total, won, lost, winRate, expectancy, total >= MinReliable));
        }
        return result;
    }
}
