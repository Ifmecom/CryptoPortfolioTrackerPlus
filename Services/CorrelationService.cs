using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, stateless correlatie-berekening (geen netwerk, geen UI-afhankelijkheden —
/// volledig unit-testbaar).
/// </summary>
public class CorrelationService : ICorrelationService
{
    // =========================================================================
    // Pearson correlatie
    // =========================================================================

    public double ComputePearson(List<OhlcvBar> a, List<OhlcvBar> b, int lookbackDays = 60)
    {
        if (a.Count < 5 || b.Count < 5) return 0;

        // Uitlijnen op datum — beide series gesorteerd op datum
        var mapA = a.TakeLast(lookbackDays + 5)
                    .ToDictionary(x => x.Date.Date, x => x.Close);
        var mapB = b.TakeLast(lookbackDays + 5)
                    .ToDictionary(x => x.Date.Date, x => x.Close);

        var commonDates = mapA.Keys
            .Intersect(mapB.Keys)
            .OrderBy(d => d)
            .ToList();

        if (commonDates.Count < 5) return 0;

        // Dagelijkse rendementen berekenen op de gemeenschappelijke datums
        var retA = new List<double>();
        var retB = new List<double>();

        for (int i = 1; i < commonDates.Count; i++)
        {
            double prevA = mapA[commonDates[i - 1]];
            double prevB = mapB[commonDates[i - 1]];
            if (prevA <= 0 || prevB <= 0) continue;

            retA.Add((mapA[commonDates[i]] - prevA) / prevA);
            retB.Add((mapB[commonDates[i]] - prevB) / prevB);
        }

        return Pearson(retA, retB);
    }

    // =========================================================================
    // Gediversifieerde shortlist
    // =========================================================================

    public List<ThreePctLiveRow> BuildDiversifiedShortlist(
        IReadOnlyList<ThreePctLiveRow>              rows,
        IReadOnlyDictionary<string, List<OhlcvBar>> barsMap,
        List<OhlcvBar>                              btcBars,
        int                                         maxPositions   = 5,
        double                                      maxCorrelation = 0.80)
    {
        // Beschouw alleen niet-gefilterde rijen, gesorteerd op expectancy
        var candidates = rows
            .Where(r => !r.IsFiltered)
            .OrderByDescending(r => r.Expectancy)
            .ThenByDescending(r => r.Score)
            .ToList();

        var selected     = new List<ThreePctLiveRow>();
        var selectedBars = new List<List<OhlcvBar>>();

        foreach (var row in candidates)
        {
            if (selected.Count >= maxPositions) break;

            var bars = barsMap.GetValueOrDefault(row.Symbol);
            if (bars is null || bars.Count < 20) continue;

            // Check correlatie met alle al geselecteerde posities
            bool tooCorrelated = selectedBars.Any(
                selBars => ComputePearson(bars, selBars) >= maxCorrelation);

            if (!tooCorrelated)
            {
                selected.Add(row);
                selectedBars.Add(bars);
            }
        }

        return selected;
    }

    // =========================================================================
    // Hulpmethoden
    // =========================================================================

    /// <summary>Berekent de Pearson-correlatiecoëfficiënt van twee even lange reeksen.</summary>
    public static double Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        int n = Math.Min(x.Count, y.Count);
        if (n < 3) return 0;

        double xMean = 0, yMean = 0;
        for (int i = 0; i < n; i++) { xMean += x[i]; yMean += y[i]; }
        xMean /= n;
        yMean /= n;

        double num = 0, denX = 0, denY = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - xMean;
            double dy = y[i] - yMean;
            num  += dx * dy;
            denX += dx * dx;
            denY += dy * dy;
        }

        double den = Math.Sqrt(denX * denY);
        return den > 1e-12 ? Math.Clamp(num / den, -1, 1) : 0;
    }

    /// <summary>
    /// Korte tekst die de correlatie beschrijft (voor UI-weergave).
    /// </summary>
    public static string CorrelationLabel(double corr) => Math.Abs(corr) switch
    {
        >= 0.90 => "Zeer hoog",
        >= 0.80 => "Hoog",
        >= 0.60 => "Matig",
        >= 0.40 => "Laag",
        _       => "Neutraal",
    };
}
