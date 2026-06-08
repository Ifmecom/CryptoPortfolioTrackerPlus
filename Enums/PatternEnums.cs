namespace CryptoPortfolioTracker.Enums;

// ── Pattern type catalogue ──────────────────────────────────────────────────

public enum PatternType
{
    // Level 1 — Indicator-based (always fast to compute)
    RsiOversold,
    RsiOverbought,
    MacdBullishCross,
    MacdBearishCross,
    EmaBullishCross,
    EmaBearishCross,
    BollingerSqueeze,
    PriceAboveEma50,
    PriceBelowEma50,
    TrendingMarket,         // ADX > 25

    // Level 2 — OHLCV swing-point analysis
    VolumeSpike,
    Uptrend,                // HH + HL series
    Downtrend,              // LH + LL series
    BullFlag,               // Sharp up move → tight consolidation
    BearFlag,               // Sharp down move → tight consolidation
    DoubleBottom,
    DoubleTop,
    AscendingTriangle,      // Flat highs + rising lows
    DescendingTriangle,     // Falling highs + flat lows
    SymmetricalTriangle,    // Converging highs and lows
    SupportBounce,          // Price near support + turning up
    ResistanceRejection,    // Price near resistance + turning down
    BreakoutAboveResistance,
    BreakdownBelowSupport,
    PotentialBreakout,      // Within 3 % of resistance (not yet confirmed)
    Consolidation,          // Low range / low ATR range

    AscendingChannel,            // Bullish — both trendlines rising in parallel
    DescendingChannel,           // Bearish — both trendlines falling in parallel
    AdamAndEve,                  // Bullish reversal — two lows: one sharp (Adam) + one rounded (Eve)

    // Level 3 — Complex classical patterns (require more bars)
    HeadAndShoulders,           // Bearish reversal — 3 peaks, middle highest, neckline break
    InverseHeadAndShoulders,    // Bullish reversal — 3 troughs, middle deepest
    RisingWedge,                // Bearish — both trend lines rising but converging
    FallingWedge,               // Bullish — both trend lines falling but converging
    CupAndHandle,               // Bullish continuation — U-shape recovery + handle consolidation
}

// ── Direction category of a pattern ────────────────────────────────────────

public enum PatternCategory
{
    Bullish,
    Bearish,
    Neutral,
    Warning,
}

// ── UI filter options ───────────────────────────────────────────────────────

public enum PatternFilter
{
    All,
    HighestScore,
    NearBreakout,
    BullishOnly,
    BearishOnly,
}
