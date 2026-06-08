using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// A single detected technical pattern on a specific timeframe.
/// Returned by <see cref="Services.IPatternDetectionService"/>.
/// </summary>
public class PatternResult
{
    /// <summary>Identifies the type of pattern.</summary>
    public PatternType Type { get; set; }

    /// <summary>Bullish, Bearish, Neutral or Warning.</summary>
    public PatternCategory Category { get; set; }

    /// <summary>Timeframe label: "1D", "4H" or "1H".</summary>
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>True when the pattern is complete and confirmed;
    /// false = early / tentative detection.</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>Pattern clarity / strength on a 0–100 scale.</summary>
    public int Strength { get; set; }

    /// <summary>Dutch plain-language description shown to the user.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Breakout, breakdown or reversal price level (if applicable).</summary>
    public double? KeyLevel { get; set; }

    /// <summary>Distance from current price to KeyLevel in percent.</summary>
    public double? DistancePct { get; set; }

    /// <summary>
    /// Optional chart overlay data (markers, necklines, trendlines).
    /// Populated by the detection methods that have geometric information.
    /// Null for indicator-only patterns (RSI, MACD, EMA …).
    /// </summary>
    public PatternAnnotation? Annotation { get; set; }

    // ── Display helpers used by PatternCoinRow ──────────────────────────────

    /// <summary>Human-readable pattern name (Dutch).</summary>
    public string DisplayName => Type switch
    {
        PatternType.RsiOversold              => "RSI Oversold",
        PatternType.RsiOverbought            => "RSI Overbought",
        PatternType.MacdBullishCross         => "MACD Bull",
        PatternType.MacdBearishCross         => "MACD Bear",
        PatternType.EmaBullishCross          => "EMA Bull Cross",
        PatternType.EmaBearishCross          => "EMA Bear Cross",
        PatternType.BollingerSqueeze         => "BB Squeeze",
        PatternType.PriceAboveEma50          => "> EMA50",
        PatternType.PriceBelowEma50          => "< EMA50",
        PatternType.TrendingMarket           => "Trending (ADX)",
        PatternType.VolumeSpike              => "Volume Spike",
        PatternType.Uptrend                  => "Opwaartse trend",
        PatternType.Downtrend                => "Neerwaartse trend",
        PatternType.BullFlag                 => "Bull Flag",
        PatternType.BearFlag                 => "Bear Flag",
        PatternType.DoubleBottom             => "Dubbele Bodem",
        PatternType.DoubleTop                => "Dubbele Top",
        PatternType.AscendingTriangle        => "Oplopende Driehoek",
        PatternType.DescendingTriangle       => "Descend. Driehoek",
        PatternType.SymmetricalTriangle      => "Sym. Driehoek",
        PatternType.SupportBounce            => "Support Bounce",
        PatternType.ResistanceRejection      => "Resistance Reject",
        PatternType.BreakoutAboveResistance  => "Breakout ↑",
        PatternType.BreakdownBelowSupport    => "Breakdown ↓",
        PatternType.PotentialBreakout        => "Bijna Breakout",
        PatternType.Consolidation            => "Consolidatie",
        PatternType.AscendingChannel         => "Oplopend Kanaal",
        PatternType.DescendingChannel        => "Dalend Kanaal",
        PatternType.AdamAndEve               => "Adam & Eve",
        PatternType.HeadAndShoulders         => "Head & Shoulders",
        PatternType.InverseHeadAndShoulders  => "Inv. H&S",
        PatternType.RisingWedge              => "Rising Wedge",
        PatternType.FallingWedge             => "Falling Wedge",
        PatternType.CupAndHandle             => "Cup & Handle",
        _                                    => Type.ToString(),
    };
}
