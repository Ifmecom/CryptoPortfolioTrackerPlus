using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Complete pattern-trading analysis for a single coin.
/// Produced by <see cref="Services.IPatternTradingService"/>.
/// </summary>
public class PatternCoinAnalysis
{
    // ── Identity ────────────────────────────────────────────────────────────
    public Coin Coin { get; set; } = null!;

    /// <summary>True when the coin has an active holding (quantity > 0) in the portfolio.</summary>
    public bool HasHolding { get; set; }

    /// <summary>True when the coin was added via the watchlist (not a portfolio holding).</summary>
    public bool IsWatchlist { get; set; }

    // ── Tradability score ───────────────────────────────────────────────────

    /// <summary>0–100 score representing the quality of the best identified trade setup.</summary>
    public int TradabilityScore { get; set; }

    /// <summary>Direction of the best setup: "Long", "Short" or "Neutraal".</summary>
    public string PrimaryDirection { get; set; } = "Neutraal";

    // ── Detected patterns ───────────────────────────────────────────────────

    /// <summary>All detected patterns across 1D, 4H and 1H timeframes.</summary>
    public List<PatternResult> Patterns { get; set; } = new();

    /// <summary>Patterns with Strength >= 60 — shown as badges in the UI.</summary>
    public List<PatternResult> KeyPatterns => Patterns.Where(p => p.Strength >= 60).ToList();

    // ── Trade setup ─────────────────────────────────────────────────────────
    public TradeSetupAdvice? Setup { get; set; }

    // ── Key levels ──────────────────────────────────────────────────────────
    public List<PatternLevel> SupportLevels    { get; set; } = new();
    public List<PatternLevel> ResistanceLevels { get; set; } = new();

    /// <summary>True when price is within 3 % of a resistance level (potential breakout zone).</summary>
    public bool IsNearBreakout { get; set; }

    // ── Timeframe summaries (for the card header badges) ───────────────────
    public string DailyBias  { get; set; } = string.Empty;   // "Bullish" / "Bearish" / "Neutraal"
    public string H4Bias     { get; set; } = string.Empty;
    public string H1Bias     { get; set; } = string.Empty;
    public string M15Bias    { get; set; } = string.Empty;
    public double DailyRsi   { get; set; }
    public double H4Rsi      { get; set; }
    public double H1Rsi      { get; set; }
    public double M15Rsi     { get; set; }

    // ── Raw OHLCV bars (stored for chart rendering) ─────────────────────────
    public List<OhlcvBar> DailyBars { get; set; } = new();
    public List<OhlcvBar> H4Bars    { get; set; } = new();
    public List<OhlcvBar> H1Bars    { get; set; } = new();
    public List<OhlcvBar> M15Bars   { get; set; } = new();

    // ── Metadata ────────────────────────────────────────────────────────────
    public bool   HasData    { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }

    // ── Share text ─────────────────────────────────────────────────────────
    /// <summary>Pre-formatted Dutch text for clipboard sharing.</summary>
    public string ShareText  { get; set; } = string.Empty;

    // ── Score label ─────────────────────────────────────────────────────────
    public string ScoreLabel => TradabilityScore switch
    {
        >= 80 => "Sterke setup",
        >= 60 => "Mogelijke setup",
        >= 40 => "In de gaten houden",
        _     => "Niet interessant",
    };
}

/// <summary>
/// A support or resistance price level with the timeframe on which it was detected,
/// used exclusively by the Pattern Trading analysis (not related to the PriceLevel EF entity).
/// </summary>
/// <param name="Price">The price of the level.</param>
/// <param name="Timeframe">The timeframe it was detected on: "1D", "4H", "1H" or "15M".</param>
public record PatternLevel(double Price, string Timeframe);
