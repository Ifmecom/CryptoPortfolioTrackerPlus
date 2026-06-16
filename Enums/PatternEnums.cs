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

    BullPennant,                // Bullish continuation — sharp pole + small converging triangle
    BearPennant,                // Bearish continuation — sharp drop + small converging triangle
}

// ── Direction category of a pattern ────────────────────────────────────────

public enum PatternCategory
{
    Bullish,
    Bearish,
    Neutral,
    Warning,
}

// ── Levenscyclus-status van een patroon (drie-staten-model, handboek §5) ─────

public enum PatternStatus
{
    /// <summary>Structuur is geldig, maar de prijs heeft het sleutelniveau nog niet bereikt.</summary>
    Forming,
    /// <summary>De live koers staat voorbij het sleutelniveau, maar geen afgesloten candle bevestigt het.</summary>
    Tentative,
    /// <summary>Een slotkoers (bars[^1].Close) sloot voorbij het sleutelniveau met de vereiste marge.</summary>
    Confirmed,
}

// ── Levenscyclus over scans heen (P7 — continue invalidatie met geheugen) ────

/// <summary>
/// De fase van een patroon dat over meerdere scans heen wordt onthouden
/// (<c>PatternStateRecord</c>). Breidt het momentane drie-staten-model
/// (<see cref="PatternStatus"/>) uit met terminale/late fases.
/// </summary>
public enum PatternLifecycle
{
    /// <summary>Structuur geldig, prijs heeft het sleutelniveau nog niet bereikt (== Forming).</summary>
    Forming     = 0,
    /// <summary>Live koers staat voorbij het sleutelniveau, nog geen afgesloten candle.</summary>
    Tentative   = 1,
    /// <summary>Een slotkoers brak het sleutelniveau met de vereiste marge (bevestigd).</summary>
    Confirmed   = 2,
    /// <summary>Na bevestiging het meetbare doel (Tmax) bereikt — uitgespeeld.</summary>
    PlayedOut   = 3,
    /// <summary>Een invalidatieregel vuurde (wand-/steun-sluiting, apex, tegenovergestelde breakout).</summary>
    Invalidated = 4,
    /// <summary>Verdwenen zonder duidelijke invalidatie (max-leeftijd / niet meer gedetecteerd na grace).</summary>
    Expired     = 5,
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
