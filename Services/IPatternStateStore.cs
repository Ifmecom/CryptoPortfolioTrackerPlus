using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Persistent geheugen van gedetecteerde patronen over scans heen (P7, Stap 4). Verzoent de verse,
/// stateless detecties van één coin met de onthouden <see cref="PatternStateRecord"/>s, slaat de
/// uitkomst op, verrijkt de detecties met hun levenscyclus en geeft de fase-overgangen terug
/// (voor notificaties, Stap 6).
/// </summary>
public interface IPatternStateStore
{
    /// <summary>
    /// Verzoen en persisteer het patroon-geheugen voor één coin. <paramref name="detections"/> wordt
    /// in-place verrijkt met <see cref="PatternResult.Lifecycle"/>, <see cref="PatternResult.TimesSeen"/>
    /// en <see cref="PatternResult.LifecycleReason"/>. Retourneert de transities van deze pas.
    /// </summary>
    Task<IReadOnlyList<PatternTransition>> ReconcileCoinAsync(
        string coinApiId,
        string coinSymbol,
        IReadOnlyList<PatternResult> detections,
        double currentPrice,
        CancellationToken ct = default);
}
