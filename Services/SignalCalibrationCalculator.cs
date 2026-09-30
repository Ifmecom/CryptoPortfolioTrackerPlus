using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Gemeten prestatie van één groep signalen (bron + richting + scoreklasse, of + regime) op één horizon.
/// "Raak" = het richting-gecorrigeerde rendement na de horizon is positief.
/// </summary>
public sealed record SignalCalibrationRow(
    string Source,
    string Direction,
    string Group,           // scoreklasse ("70–79") of regime ("RiskOn")
    int    SortKey,
    int    HorizonDays,
    int    Count,
    int    Hits,
    double HitRatePct,
    double AvgReturnPct,
    double MedianReturnPct,
    double AvgMfePct,
    double AvgMaePct,
    bool   IsReliable)
{
    public string HitRateDisplay   => Count > 0 ? $"{HitRatePct:0}%" : "–";
    public string AvgReturnDisplay => Count > 0 ? $"{AvgReturnPct:+0.0;-0.0;0.0}%" : "–";
    public string MedianDisplay    => Count > 0 ? $"{MedianReturnPct:+0.0;-0.0;0.0}%" : "–";
    public string MfeMaeDisplay    => Count > 0 ? $"+{AvgMfePct:0.0} / -{AvgMaePct:0.0}" : "–";
    public string CountDisplay     => IsReliable || Count == 0 ? $"{Count}" : $"{Count} ⚠";
    public string DirectionLabel   => Direction == "Long" ? "▲ Long" : "▼ Short";
}

/// <summary>
/// Pure kalibratie van signaalscores op échte uitkomsten (v1.46) — de ontbrekende "signal-outcome-tracker"
/// uit PRD §13. Groepeert gemeten <see cref="SignalOutcome"/>s per bron, richting en scoreklasse en
/// berekent per horizon de trefkans (% in de goede richting), het gemiddelde en mediane rendement en de
/// gemiddelde mee-/tegenbeweging. Zo wordt een score (bv. "72, Long") vertaald naar een gemeten kans.
/// </summary>
public static class SignalCalibrationCalculator
{
    /// <summary>Minimum aantal metingen per groep voor een betrouwbare uitspraak (zie <see cref="ReliabilityThresholds"/>).</summary>
    public const int MinReliable = ReliabilityThresholds.MinSignalOutcomes;

    /// <summary>Standaard-horizon voor de "gemeten kans" naast een signaal.</summary>
    public const int DefaultHorizonDays = 7;

    // ── Scoreklassen ────────────────────────────────────────────────────────

    /// <summary>
    /// Scoreklassen per bron en richting, van zwak naar sterk. SignalEngine-scores zijn symmetrisch
    /// rond 50 (Long ≥ 60, Short ≤ 40); de Pattern-TradabilityScore is een sterkte (hoger = beter,
    /// richting apart), dus die gebruikt dezelfde klassen voor Long en Short.
    /// </summary>
    public static IReadOnlyList<string> BucketsFor(string source, string direction)
        => source == SignalOutcomeSources.Pattern
            ? new[] { "40–59", "60–79", "80–100" }
            : direction == "Short"
                ? new[] { "31–40", "21–30", "0–20" }
                : new[] { "60–69", "70–79", "80–100" };

    public static string BucketFor(string source, string direction, double score)
    {
        if (source == SignalOutcomeSources.Pattern)
            return score < 60 ? "40–59" : score < 80 ? "60–79" : "80–100";

        if (direction == "Short")
            return score > 30 ? "31–40" : score > 20 ? "21–30" : "0–20";

        return score < 70 ? "60–69" : score < 80 ? "70–79" : "80–100";
    }

    // ── Berekening ──────────────────────────────────────────────────────────

    /// <summary>
    /// Kalibratietabel voor één bron op één horizon: per richting (Long, dan Short) alle scoreklassen,
    /// ook lege (Count 0), zodat de UI een volledige tabel kan tonen.
    /// </summary>
    public static List<SignalCalibrationRow> Compute(
        IEnumerable<SignalOutcome> outcomes, string source, int horizonDays)
    {
        var measured = Measured(outcomes, source, horizonDays);
        var result   = new List<SignalCalibrationRow>();

        foreach (var dir in new[] { "Long", "Short" })
        {
            var buckets = BucketsFor(source, dir);
            for (int i = 0; i < buckets.Count; i++)
            {
                string bucket = buckets[i];
                var group = measured
                    .Where(o => o.Direction == dir && BucketFor(source, dir, o.Score) == bucket)
                    .ToList();
                result.Add(BuildRow(source, dir, bucket, i, horizonDays, group));
            }
        }
        return result;
    }

    /// <summary>Uitsplitsing per BTC-marktregime (richting apart), voor één bron op één horizon.</summary>
    public static List<SignalCalibrationRow> ComputeByRegime(
        IEnumerable<SignalOutcome> outcomes, string source, int horizonDays)
    {
        var measured = Measured(outcomes, source, horizonDays);
        var regimes  = new[] { "RiskOn", "Neutral", "RiskOff" };
        var result   = new List<SignalCalibrationRow>();

        foreach (var dir in new[] { "Long", "Short" })
        {
            for (int i = 0; i < regimes.Length; i++)
            {
                var group = measured
                    .Where(o => o.Direction == dir && o.MarketRegime == regimes[i])
                    .ToList();
                if (group.Count == 0) continue;
                result.Add(BuildRow(source, dir, regimes[i], i, horizonDays, group));
            }
        }
        return result;
    }

    /// <summary>Totaal per richting (alle scoreklassen samen) — voor de samenvattingskaarten.</summary>
    public static SignalCalibrationRow Total(
        IEnumerable<SignalOutcome> outcomes, string source, string direction, int horizonDays)
    {
        var group = Measured(outcomes, source, horizonDays).Where(o => o.Direction == direction).ToList();
        return BuildRow(source, direction, "Totaal", 0, horizonDays, group);
    }

    /// <summary>Zoekt de kalibratierij die bij een (nieuw) signaal hoort; <c>null</c> bij Flat/onbekend.</summary>
    public static SignalCalibrationRow? Lookup(
        IEnumerable<SignalCalibrationRow> rows, string source, string direction, double score)
    {
        if (SignalOutcomeEvaluator.DirectionSign(direction) == 0) return null;
        string bucket = BucketFor(source, direction, score);
        return rows.FirstOrDefault(r => r.Source == source && r.Direction == direction && r.Group == bucket);
    }

    /// <summary>Korte tekst voor naast een signaal: "58% · 43" of "n=4" (te weinig data) of "–".</summary>
    public static string ShortText(SignalCalibrationRow? row)
    {
        if (row is null || row.Count == 0) return "–";
        return row.IsReliable ? $"{row.HitRatePct:0}% · {row.Count}" : $"n={row.Count}";
    }

    /// <summary>Uitleg-tooltip bij de gemeten kans.</summary>
    public static string Explanation(SignalCalibrationRow? row)
    {
        if (row is null)
            return "Geen gemeten kans: alleen Long- en Short-signalen worden gevolgd.";
        if (row.Count == 0)
            return $"Nog geen gemeten {row.Direction}-signalen in scoreklasse {row.Group}. " +
                   "Uitkomsten worden automatisch bijgehouden zodra signalen oud genoeg zijn.";

        string text =
            $"Historisch lag {row.HitRatePct:0}% van {row.Count} vergelijkbare {row.Direction}-signalen " +
            $"(score {row.Group}) na {row.HorizonDays} dagen in de goede richting. " +
            $"Gemiddeld rendement {row.AvgReturnPct:+0.0;-0.0;0.0}%, mediaan {row.MedianReturnPct:+0.0;-0.0;0.0}%.";

        if (!row.IsReliable)
            text += $" Let op: nog te weinig metingen voor een betrouwbare uitspraak (minimaal {MinReliable}).";

        return text;
    }

    // ── Hulpfuncties ────────────────────────────────────────────────────────

    private static List<SignalOutcome> Measured(IEnumerable<SignalOutcome> outcomes, string source, int horizonDays)
        => outcomes
            .Where(o => o.Source == source && o.ReturnFor(horizonDays).HasValue)
            .ToList();

    private static SignalCalibrationRow BuildRow(
        string source, string direction, string group, int sortKey, int horizonDays,
        IReadOnlyList<SignalOutcome> items)
    {
        if (items.Count == 0)
            return new SignalCalibrationRow(source, direction, group, sortKey, horizonDays,
                0, 0, 0, 0, 0, 0, 0, false);

        var returns = items.Select(o => o.ReturnFor(horizonDays)!.Value).ToList();
        int hits    = returns.Count(r => r > 0);

        var mfe = items.Where(o => o.MaxFavorablePct.HasValue).Select(o => o.MaxFavorablePct!.Value).ToList();
        var mae = items.Where(o => o.MaxAdversePct.HasValue).Select(o => o.MaxAdversePct!.Value).ToList();

        return new SignalCalibrationRow(
            source, direction, group, sortKey, horizonDays,
            Count:           items.Count,
            Hits:            hits,
            HitRatePct:      100.0 * hits / items.Count,
            AvgReturnPct:    returns.Average(),
            MedianReturnPct: Median(returns),
            AvgMfePct:       mfe.Count > 0 ? mfe.Average() : 0,
            AvgMaePct:       mae.Count > 0 ? mae.Average() : 0,
            IsReliable:      items.Count >= MinReliable);
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
