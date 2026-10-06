using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Bij Richting "Both" scoort de live-scan elke coin Long én Short. Omdat de gemeten
/// hitrate per scoreklasse geldt (niet per richting), kregen beide kanten dezelfde
/// hitrate/expectancy en stonden ze allebei in de top. Hier blijft per coin één richting
/// over: eerst een gekwalificeerde (niet door F6/F7 gefilterde), daarna de hoogste score.
/// </summary>
public static class ThreePctBiasSelector
{
    public static List<ThreePctLiveRow> BestPerCoin(IEnumerable<ThreePctLiveRow> rows) =>
        rows.GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderBy(r => r.IsFiltered)
                .ThenByDescending(r => r.Score)
                .ThenByDescending(r => r.Expectancy)
                .First())
            .ToList();
}
