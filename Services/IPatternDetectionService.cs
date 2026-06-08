using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Detects technical chart patterns from OHLCV data and pre-computed indicator values.
/// All methods are synchronous and pure (no I/O) — call from a background thread.
/// </summary>
public interface IPatternDetectionService
{
    /// <summary>
    /// Detects OHLCV-based patterns (swing highs/lows, flags, triangles, volume spikes, etc.)
    /// from raw candlestick data for a single timeframe.
    /// </summary>
    List<PatternResult> DetectFromBars(List<OhlcvBar> bars, string timeframeLabel, double currentPrice);

    /// <summary>
    /// Detects indicator-based patterns (RSI levels, EMA cross, MACD, Bollinger Squeeze, etc.)
    /// from a pre-computed <see cref="TimeframeAnalysis"/> object.
    /// </summary>
    List<PatternResult> DetectFromIndicators(TimeframeAnalysis tf, string timeframeLabel, double currentPrice);

    /// <summary>
    /// Calculates a 0–100 tradability score and the primary direction from a full set of patterns
    /// and a daily TimeframeAnalysis.
    /// </summary>
    (int score, string direction) CalculateTradabilityScore(
        List<PatternResult> patterns,
        TimeframeAnalysis   daily,
        TimeframeAnalysis   h4);
}
