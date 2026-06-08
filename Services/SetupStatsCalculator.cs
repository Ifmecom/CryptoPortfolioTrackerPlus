using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure-statische berekeningen voor de "Setup Strategie" statistieken.
/// Geen UI-, EF- of LiveCharts-afhankelijkheden — volledig unit-testbaar.
/// </summary>
public static class SetupStatsCalculator
{
    // =========================================================================
    // Samenvatting (KPI-kaarten)
    // =========================================================================

    /// <summary>Geaggregeerde statistieken over een set gesloten setups.</summary>
    public sealed record SummaryStats(
        int    TotalClosed,
        int    Won,
        int    Lost,
        int    Open,
        int    Tp2Won,
        double WinRatePct,
        double Tp2RatePct,
        double ProfitFactor,
        double ExpectancyPct,
        double AvgPnlPct,
        double AvgHoldHours);

    /// <summary>
    /// Berekent de kernstatistieken uit de opgegeven lijsten.
    /// </summary>
    /// <param name="closed">Setups met status Won of Lost.</param>
    /// <param name="all">Alle setups (voor open-telling).</param>
    public static SummaryStats Compute(
        IReadOnlyList<WatchedSetup> closed,
        IReadOnlyList<WatchedSetup> all)
    {
        var won  = closed.Where(s => s.Status == WatchedSetupStatus.Won).ToList();
        var lost = closed.Where(s => s.Status == WatchedSetupStatus.Lost).ToList();
        int total = won.Count + lost.Count;

        int tp2Won = won.Count(s => s.Tp2Hit);
        int open   = all.Count(s => s.Status is WatchedSetupStatus.Open
                                             or WatchedSetupStatus.Watching);

        double winRatePct = total > 0 ? 100.0 * won.Count / total : 0;
        double tp2RatePct = won.Count > 0 ? 100.0 * tp2Won / won.Count : 0;

        var wonPnl  = won.Where(s => s.PnlPct.HasValue).Select(s => s.PnlPct!.Value).ToList();
        var lostPnl = lost.Where(s => s.PnlPct.HasValue).Select(s => s.PnlPct!.Value).ToList();

        double grossWin  = wonPnl.Sum();
        double grossLoss = Math.Abs(lostPnl.Sum());
        double pf = grossLoss > 0 ? grossWin / grossLoss
                  : grossWin  > 0 ? double.PositiveInfinity
                  : 0;

        double avgWinPnl  = wonPnl.Count  > 0 ? wonPnl.Average()  : 0;
        double avgLossPnl = lostPnl.Count > 0 ? lostPnl.Average() : 0;

        double expectancy = 0;
        if (total > 0 && (wonPnl.Count > 0 || lostPnl.Count > 0))
        {
            double wr = winRatePct / 100.0;
            expectancy = wr * avgWinPnl - (1.0 - wr) * Math.Abs(avgLossPnl);
        }

        var allPnl = closed.Where(s => s.PnlPct.HasValue).Select(s => s.PnlPct!.Value).ToList();
        double avgPnl = allPnl.Count > 0 ? allPnl.Average() : 0;

        var withTime = closed.Where(s => s.ClosedAt.HasValue).ToList();
        double avgHold = withTime.Count > 0
            ? withTime.Average(s => (s.ClosedAt!.Value - s.AddedAt).TotalHours)
            : 0;

        return new SummaryStats(
            TotalClosed: closed.Count,
            Won: won.Count, Lost: lost.Count, Open: open, Tp2Won: tp2Won,
            WinRatePct: winRatePct, Tp2RatePct: tp2RatePct,
            ProfitFactor: pf, ExpectancyPct: expectancy,
            AvgPnlPct: avgPnl, AvgHoldHours: avgHold);
    }

    // =========================================================================
    // Uitsplitsing per groep (richting / score-klasse / regime)
    // =========================================================================

    /// <summary>Statistieken voor één groep (bijv. "Long", "Score 65–79", "RiskOn").</summary>
    public sealed record GroupStats(
        string Label,
        int    Count,
        int    Won,
        int    Lost,
        double WinRatePct,
        double AvgPnlPct);

    /// <summary>Groepeert <paramref name="closed"/> op de opgegeven sleutel en berekent per groep statistieken.</summary>
    public static List<GroupStats> GroupBy(
        IReadOnlyList<WatchedSetup> closed,
        Func<WatchedSetup, string> keySelector)
    {
        return closed
            .GroupBy(keySelector)
            .Select(g =>
            {
                int cnt   = g.Count();
                int won   = g.Count(s => s.Status == WatchedSetupStatus.Won);
                int lost  = g.Count(s => s.Status == WatchedSetupStatus.Lost);
                double wr = cnt > 0 ? 100.0 * won / cnt : 0;
                var pnlList = g.Where(s => s.PnlPct.HasValue).Select(s => s.PnlPct!.Value).ToList();
                double avg  = pnlList.Count > 0 ? pnlList.Average() : 0;
                return new GroupStats(g.Key, cnt, won, lost, wr, avg);
            })
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    // =========================================================================
    // Score-klasse helpers
    // =========================================================================

    /// <summary>Geeft het score-label voor een setup-score (bijv. 72 → "65–79").</summary>
    public static string ScoreBucket(int score) =>
        score < 50 ? "< 50" :
        score < 65 ? "50–64" :
        score < 80 ? "65–79" : "80+";

    /// <summary>Vaste volgorde van score-buckets (voor X-as van de grafiek).</summary>
    public static readonly string[] ScoreBucketOrder = { "< 50", "50–64", "65–79", "80+" };

    // =========================================================================
    // Cumulatieve equity-curve
    // =========================================================================

    /// <summary>
    /// Berekent de cumulatieve PnL% na elk gesloten setup, gesorteerd op sluitdatum.
    /// Resultaat[0] = PnL na de eerste setup, resultaat[N-1] = totale PnL.
    /// Setups zonder PnlPct worden overgeslagen.
    /// </summary>
    public static List<double> CumulativePnl(IReadOnlyList<WatchedSetup> closed)
    {
        var sorted = closed
            .Where(s => s.PnlPct.HasValue)
            .OrderBy(s => s.ClosedAt ?? s.AddedAt)
            .ToList();

        var result = new List<double>(sorted.Count);
        double running = 0;
        foreach (var s in sorted)
        {
            running += s.PnlPct!.Value;
            result.Add(Math.Round(running, 2));
        }
        return result;
    }

    // =========================================================================
    // Win/Lost count per score bucket (voor staafdiagram)
    // =========================================================================

    /// <summary>
    /// Geeft voor elk score-bucket (in de vaste volgorde van <see cref="ScoreBucketOrder"/>)
    /// de aantallen Won en Lost terug.
    /// Retourneert twee arrays van dezelfde lengte als <see cref="ScoreBucketOrder"/>.
    /// </summary>
    public static (double[] WonByBucket, double[] LostByBucket) ScoreBucketCounts(
        IReadOnlyList<WatchedSetup> closed)
    {
        var won   = new double[ScoreBucketOrder.Length];
        var lost  = new double[ScoreBucketOrder.Length];
        var index = ScoreBucketOrder
            .Select((label, i) => (label, i))
            .ToDictionary(t => t.label, t => t.i);

        foreach (var s in closed)
        {
            var bucket = ScoreBucket(s.Score);
            if (!index.TryGetValue(bucket, out int idx)) continue;

            if (s.Status == WatchedSetupStatus.Won)  won[idx]++;
            if (s.Status == WatchedSetupStatus.Lost) lost[idx]++;
        }
        return (won, lost);
    }
}
