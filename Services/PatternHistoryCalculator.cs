using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>Hit/fail-statistiek voor één patroontype, afgeleid uit de PatternStates-levenscyclus.</summary>
public sealed record PatternHistoryStat(
    PatternType Type,
    int PlayedOut,
    int Invalidated,
    int Expired,
    int StillActive,
    int Decisive,       // PlayedOut + Invalidated (patronen die beslissend afliepen)
    double HitRate,     // PlayedOut / Decisive (0–1)
    bool IsReliable)
{
    public string DisplayName => PatternResult.NameFor(Type);
    public double HitRatePct  => HitRate * 100;
}

/// <summary>
/// Pure analytics over het patroon-geheugen (P7, item 7). Berekent per patroontype hoe vaak een
/// bevestigd patroon zijn doel uitspeelde (PlayedOut) versus terugviel (Invalidated) — de basis voor
/// een latere score-kalibratie op echte uitkomsten.
/// </summary>
public static class PatternHistoryCalculator
{
    /// <summary>Minimaal aantal beslissende uitkomsten om een hit-rate als betrouwbaar te tonen.</summary>
    public const int MinDecisiveForReliable = ReliabilityThresholds.MinDecisive;

    public static IReadOnlyList<PatternHistoryStat> Compute(IEnumerable<PatternStateRecord> records)
        => records
            .GroupBy(r => r.Type)
            .Select(g =>
            {
                int played   = g.Count(r => r.Lifecycle == PatternLifecycle.PlayedOut);
                int inval    = g.Count(r => r.Lifecycle == PatternLifecycle.Invalidated);
                int expired  = g.Count(r => r.Lifecycle == PatternLifecycle.Expired);
                int active   = g.Count(r => r.IsActive);
                int decisive = played + inval;
                double hit   = decisive > 0 ? (double)played / decisive : 0;
                return new PatternHistoryStat(
                    g.Key, played, inval, expired, active, decisive, hit,
                    decisive >= MinDecisiveForReliable);
            })
            .OrderByDescending(s => s.Decisive)
            .ThenByDescending(s => s.HitRate)
            .ToList();
}
