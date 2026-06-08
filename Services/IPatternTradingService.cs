using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Orchestrates pattern analysis for portfolio coins.
/// Fetches OHLCV data, runs <see cref="IPatternDetectionService"/> and
/// aggregates results into scored <see cref="PatternCoinAnalysis"/> objects.
/// </summary>
public interface IPatternTradingService
{
    /// <summary>
    /// Analyses all coins that have an active holding (quantity > 0) in the
    /// current portfolio.  Coins are analysed in parallel (max 3 concurrent
    /// to avoid exchange rate-limits).
    /// </summary>
    /// <param name="progress">Optional callback — called with 0..100 as each coin completes.</param>
    Task<List<PatternCoinAnalysis>> AnalyzePortfolioAsync(
        IProgress<int>?     progress    = null,
        CancellationToken   ct          = default);

    /// <summary>Analyses a single coin regardless of portfolio membership.</summary>
    Task<PatternCoinAnalysis> AnalyzeCoinAsync(Coin coin);
}
