namespace CryptoPortfolioTracker.Models;

/// <summary>Complete multi-timeframe trade analysis for a single coin.</summary>
public class TradeAnalysisResult
{
    public string   CoinName             { get; set; } = string.Empty;
    public string   Symbol               { get; set; } = string.Empty;
    public string   ImageUri             { get; set; } = string.Empty;
    public double   CurrentPrice         { get; set; }
    public double   Change24h            { get; set; }
    public int      CombinedScore        { get; set; }
    public string   Direction            { get; set; } = string.Empty;
    public string   Regime               { get; set; } = string.Empty;
    public DateTime GeneratedAt          { get; set; }
    public bool     BinanceDataAvailable { get; set; }
    public string   BinanceSymbol        { get; set; } = string.Empty;

    /// <summary>"Binance" / "KuCoin" / "lokale cache" — set by TradeAnalysisService.</summary>
    public string   DataSource           { get; set; } = string.Empty;

    public TimeframeAnalysis Weekly      { get; set; } = new();
    public TimeframeAnalysis Daily       { get; set; } = new();
    public TimeframeAnalysis FourHour   { get; set; } = new();
    public TimeframeAnalysis OneHour    { get; set; } = new();
    public TimeframeAnalysis FifteenMin { get; set; } = new();

    public List<double> ResistanceLevels { get; set; } = new();
    public List<double> SupportLevels    { get; set; } = new();
    public TradeSetupAdvice Setup        { get; set; } = new();

    // ── Verrijking (gedeeld met 3%-trading): liquiditeit, positionering, events ──

    /// <summary>Orderboek-snapshot (spread + diepte). Null = niet beschikbaar.</summary>
    public OrderBookSnapshot?   OrderBook    { get; set; }

    /// <summary>Futures-positionering (funding/OI/long-short). Null = spot-only.</summary>
    public FuturesPositioning?  Positioning  { get; set; }

    /// <summary>Macro-events binnen de handelshorizon (FOMC/CPI/NFP/PCE).</summary>
    public List<MacroEvent>     MacroEvents  { get; set; } = new();

    // ── Display helpers voor de verrijkingssectie ────────────────────────────

    public string LiquidityDisplay => OrderBook is null
        ? "n/v"
        : $"spread {OrderBook.SpreadPct:0.000}% · diepte ${OrderBook.MinDepthUsdt:N0}";

    public string FundingDisplay => Positioning is { IsAvailable: true }
        ? $"{Positioning.FundingRatePct:+0.000;-0.000}% · L/S {Positioning.LongShortRatio:F2}"
        : "n/v (spot)";

    public bool HasEventRisk => MacroEvents.Count > 0;
}

/// <summary>Indicators and observations for a single timeframe.</summary>
public class TimeframeAnalysis
{
    public string Label         { get; set; } = string.Empty;
    public bool   HasData       { get; set; }

    // Core indicators
    public double Rsi           { get; set; }
    public double Macd          { get; set; }
    public double MacdSignal    { get; set; }
    public double Ema9          { get; set; }
    public double Ema21         { get; set; }
    public double Ema50         { get; set; }
    public double Ema200        { get; set; }
    public double PctB          { get; set; }
    public double Adx           { get; set; }
    public double Atr           { get; set; }
    public bool   IsSqueeze     { get; set; }

    // Derived
    public string TrendBias     { get; set; } = "–";  // "Bullish" / "Bearish" / "Neutraal"
    public string EmaCrossState { get; set; } = "–";  // "Bullish kruis" / "Bearish kruis" / "EMA9 boven EMA21" / "EMA9 onder EMA21"

    /// <summary>Human-readable observations in Dutch, shown as bullet points.</summary>
    public List<string> Bullets { get; set; } = new();
}

/// <summary>Concrete trade setup recommendation derived from the analysis.</summary>
public class TradeSetupAdvice
{
    public string Direction    { get; set; } = string.Empty;  // "Long" / "Short" / "Geen signaal"
    public double EntryPrice   { get; set; }
    public string EntryNote    { get; set; } = string.Empty;
    public double StopLoss     { get; set; }
    public double StopLossPct  { get; set; }
    public double Target1      { get; set; }
    public double Target1Pct   { get; set; }
    public double Target2      { get; set; }
    public double Target2Pct   { get; set; }
    public double RiskReward1  { get; set; }
    public double RiskReward2  { get; set; }
    public string Confidence   { get; set; } = string.Empty;  // "Laag" / "Gemiddeld" / "Hoog"
    public List<string> Reasoning { get; set; } = new();

    /// <summary>
    /// False wanneer de SL/TP-niveaus richtingsgewijs ongeldig of degenerate zijn
    /// (bijv. ATR=0 → SL=Entry). Gezet door TradeSetupValidator bij generatie.
    /// </summary>
    public bool   IsValid           { get; set; } = true;

    /// <summary>Waarschuwing bij ongeldige niveaus of krappe R/R (&lt; 1,5:1). Leeg = ok.</summary>
    public string ValidationWarning { get; set; } = string.Empty;
}
