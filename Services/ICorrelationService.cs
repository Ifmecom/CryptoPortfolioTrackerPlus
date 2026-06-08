using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Berekent de prijscorrelatie tussen coins op basis van dagelijkse rendementen
/// en bouwt een gediversifieerde shortlist van setups.
/// </summary>
public interface ICorrelationService
{
    /// <summary>
    /// Pearson-correlatie tussen de dagelijkse rendementen van twee OHLCV-reeksen.
    /// Uitgelijnde overlap (common dates). Geeft 0.0 terug bij onvoldoende data.
    /// </summary>
    double ComputePearson(
        List<OhlcvBar> a,
        List<OhlcvBar> b,
        int            lookbackDays = 60);

    /// <summary>
    /// Bouwt een gediversifieerde shortlist uit de opgegeven rijen:
    /// sorteert op expectancy, voegt daarna alleen rijen toe waarvan de
    /// correlatie met reeds geselecteerde rijen onder <paramref name="maxCorrelation"/> ligt.
    /// </summary>
    List<ThreePctLiveRow> BuildDiversifiedShortlist(
        IReadOnlyList<ThreePctLiveRow>                rows,
        IReadOnlyDictionary<string, List<OhlcvBar>>   barsMap,
        List<OhlcvBar>                                btcBars,
        int                                           maxPositions   = 5,
        double                                        maxCorrelation = 0.80);
}
