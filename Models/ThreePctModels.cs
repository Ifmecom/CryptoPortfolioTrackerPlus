namespace CryptoPortfolioTracker.Models;

// ─────────────────────────────────────────────────────────────────────────────
// 3% Trading — data-modellen (geen WinUI-afhankelijkheden)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Resultaat van het scoren van één coin met het 5-factor model (Sprint A).</summary>
public sealed class ThreePctScoreResult
{
    public string Symbol        { get; init; } = string.Empty;
    public string CoinName      { get; init; } = string.Empty;

    /// <summary>"Long" of "Short"</summary>
    public string Bias          { get; init; } = "Long";

    /// <summary>Totaalscore 0-100 (gewogen gemiddelde van 5 factoren, hernormeerd naar 100).</summary>
    public double TotalScore    { get; init; }

    // Factor-scores 0-10 (5 = neutraal, 10 = maximaal gunstig voor de opgegeven Bias)
    public double F1Trend       { get; init; }
    public double F2Momentum    { get; init; }
    public double F3Volume      { get; init; }
    public double F4Volatility  { get; init; }
    public double F5SR          { get; init; }
    // F6 Liquiditeit en F7 Positionering: Sprint B

    /// <summary>Entry-prijs op het moment van beoordeling (laatste close).</summary>
    public double EntryPrice    { get; init; }

    /// <summary>Structureel stop-loss: entry ± 1,5 × ATR (minimum-afstand begrensd).</summary>
    public double StopLoss      { get; init; }

    /// <summary>Take-profit: entry ± bruto-TP% (3% netto + fees + slippage).</summary>
    public double TakeProfit    { get; init; }

    /// <summary>R/R = |TP-entry| / |entry-SL|.</summary>
    public double RiskReward    { get; init; }

    public double Atr           { get; init; }

    // ── Sprint B: F6 Liquiditeit + F7 Positionering (gatekeepers) ─────────────

    /// <summary>F6 score 0-10 (5 = neutraal/niet beschikbaar).</summary>
    public double F6Liquidity    { get; init; } = 5.0;
    /// <summary>F7 score 0-10 (5 = neutraal/niet beschikbaar).</summary>
    public double F7Positioning  { get; init; } = 5.0;
    public bool   F6Available    { get; init; }
    public bool   F7Available    { get; init; }

    /// <summary>
    /// False als F6 of F7 onder de minimumdrempel valt (setup wordt gefilterd).
    /// F6 &lt; 4 = liquiditeitsrisico. F7 &lt; 3 = extreme positionering.
    /// </summary>
    public bool   IsQualified    { get; init; } = true;
    public string FilterReason   { get; init; } = string.Empty;

    /// <summary>Korte tekst met factorscores voor tooltip.</summary>
    public string FactorSummary { get; init; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Gecalibreerde statistieken voor één scoreklasse uit de backtest.</summary>
public sealed class ScoreClassCalibration
{
    /// <summary>"0-40" / "41-60" / "61-80" / "81-100"</summary>
    public string   ScoreClass    { get; set; } = string.Empty;
    public int      TradeCount    { get; set; }

    /// <summary>Percentage trades dat netto-TP haalde vóór SL.</summary>
    public double   HitratePct    { get; set; }

    /// <summary>Gemiddeld behaald R-multiple per trade.</summary>
    public double   AvgRMultiple  { get; set; }

    /// <summary>(WR × gem. win-R) − (LR × 1) = verwachte opbrengst per trade in R.</summary>
    public double   Expectancy    { get; set; }

    public string   Timeframe     { get; set; } = string.Empty;
    public string   Bias          { get; set; } = "Long";
    public DateTime CalibratedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>True als TradeCount >= 30 (voldoende data om op te vertrouwen).</summary>
    public bool     IsReliable    { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Instellingen voor backtest-simulatie en live-beoordeling.</summary>
public sealed class BacktestParameters
{
    /// <summary>Binance-interval: "1d" / "4h" / "1h".</summary>
    public string Timeframe      { get; set; } = "1d";

    /// <summary>Netto target in % (standaard 3%).</summary>
    public double TpNetPct       { get; set; } = 3.0;

    /// <summary>Fee per zijde in % (maker/taker, standaard 0,1%).</summary>
    public double FeePct         { get; set; } = 0.1;

    /// <summary>Geschatte slippage in % (standaard 0,05%).</summary>
    public double SlippagePct    { get; set; } = 0.05;

    /// <summary>SL-afstand = SLAtrMultiple × ATR (standaard 1,5×).</summary>
    public double SLAtrMultiple  { get; set; } = 1.5;

    /// <summary>Minimale SL-afstand in % van entry (vloer zodat SL niet te krap is).</summary>
    public double SLMinPct       { get; set; } = 1.0;

    /// <summary>Max. aantal forward-bars voor TP/SL-check; daarna timeout (geen trade).</summary>
    public int    MaxHorizonBars { get; set; } = 15;

    /// <summary>"Long" / "Short" / "Both"</summary>
    public string Bias           { get; set; } = "Long";

    /// <summary>Bruto TP = netto TP + 2 × fee (beide zijden) + slippage.</summary>
    public double TpGrossPct => TpNetPct + 2 * FeePct + SlippagePct;
}

// ─────────────────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────────
// Sprint B — externe data-snapshots
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Momentopname van het orderboek (top-5 bids + asks).</summary>
public sealed record OrderBookSnapshot(
    string Symbol,
    /// <summary>Bid-ask spread als % van de middenprijs.</summary>
    double SpreadPct,
    /// <summary>Totale USDT-waarde van de 5 beste biedingen.</summary>
    double BidDepthUsdt,
    /// <summary>Totale USDT-waarde van de 5 beste vraagprijzen.</summary>
    double AskDepthUsdt)
{
    /// <summary>Laagste diepte van beide zijden (de beperkende kant).</summary>
    public double MinDepthUsdt => Math.Min(BidDepthUsdt, AskDepthUsdt);
}

/// <summary>Futures-positioneringsdata: funding, OI en long/short-verhouding.</summary>
public sealed record FuturesPositioning(
    string Symbol,
    /// <summary>Actuele funding rate in %. Typisch -0,1% tot +0,1%.</summary>
    double FundingRatePct,
    /// <summary>Open interest in basismunt (bijv. BTC).</summary>
    double OpenInterest,
    /// <summary>Longs / shorts (> 1 = meer longs dan shorts).</summary>
    double LongShortRatio,
    /// <summary>False voor spot-only coins zonder futures-markt.</summary>
    bool   IsAvailable);

/// <summary>
/// Terugkerend macro-economisch event dat koersen kan beïnvloeden.
/// Bronnen: FOMC-vergaderingen (hardcoded 2025-2026), NFP (eerste vrijdag per maand),
/// US CPI / PCE (benadering — check officiële bronnen).
/// </summary>
public sealed record MacroEvent(
    string   Type,
    DateTime Date,
    string   Description)
{
    public string ShortDisplay => $"{Type} — {Date:dd-MM-yyyy} ({Description})";
}

/// <summary>Globale marktdata van CoinGecko /global.</summary>
public sealed record GlobalMarketData(
    double BtcDominancePct,
    double TotalMarketCapUsdt,
    double TotalVolumeUsdt);

/// <summary>Verrijkt marktregime-context voor de 3%-trading-tool.</summary>
public sealed record MarketRegimeContext(
    /// <summary>Huidig regime: RiskOn / Neutral / RiskOff.</summary>
    CryptoPortfolioTracker.Enums.MarketRegime Regime,
    /// <summary>EMA50/200-status: "Golden Cross" / "Death Cross" / "Gemengd".</summary>
    string EmaStatus,
    double BtcDominancePct,
    double BtcRsi,
    double BtcPrice,
    double Ema50,
    double Ema200)
{
    public string Summary =>
        $"{Regime} — {EmaStatus}, dominantie {BtcDominancePct:0.0}%, RSI {BtcRsi:0.0}";

    public string DominanceLabel => BtcDominancePct switch
    {
        > 60 => "Hoog (>60%) — alts zwak",
        > 50 => "Matig hoog (>50%)",
        > 40 => "Normaal",
        _    => "Laag (<40%) — altseason",
    };
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Één rij in de live-scan overzichtstabel.</summary>
public sealed class ThreePctLiveRow
{
    public string Symbol          { get; init; } = string.Empty;
    public string CoinName        { get; init; } = string.Empty;
    public double Score           { get; init; }
    public string ScoreClass      { get; init; } = string.Empty;
    public double HistHitrate     { get; init; }   // uit kalibratietabel
    public double Expectancy      { get; init; }   // uit kalibratietabel
    public string Bias            { get; init; } = "Long";
    public double EntryPrice      { get; init; }
    public double StopLoss        { get; init; }
    public double TakeProfit      { get; init; }
    public double RiskReward      { get; init; }
    public string FactorBreakdown { get; init; } = string.Empty;
    public bool   IsReliable      { get; init; }

    // ── Sprint B: gatekeeper-kwalificatie ────────────────────────────────────
    public double F6Score         { get; init; } = 5.0;
    public double F7Score         { get; init; } = 5.0;
    public bool   IsFiltered      { get; init; }
    public string FilterReason    { get; init; } = string.Empty;

    // ── Sprint C: correlatie en diversificatie ────────────────────────────────
    /// <summary>Pearson-correlatie met BTC dagrendementen (60 bars). NaN = niet berekend.</summary>
    public double BtcCorrelation        { get; init; } = double.NaN;
    /// <summary>True als dit een van de top-5 gediversifieerde setups is.</summary>
    public bool   IsDiversifiedPick     { get; init; }

    // ── Display helpers ──────────────────────────────────────────────────────

    public string ScoreDisplay      => $"{Score:0.0}";
    public string HitrateDisplay    => HistHitrate > 0 ? $"{HistHitrate:0.0}%" : "–";
    public string ExpectancyDisplay => Expectancy != 0 ? $"{Expectancy:+0.00;-0.00}R" : "–";
    public string EntryDisplay      => EntryPrice > 0 ? Fmt(EntryPrice) : "–";
    public string SLDisplay         => StopLoss > 0   ? Fmt(StopLoss)   : "–";
    public string TPDisplay         => TakeProfit > 0 ? Fmt(TakeProfit) : "–";
    public string RRDisplay         => RiskReward > 0 ? $"{RiskReward:F2} : 1" : "–";
    public string ReliableDisplay   => IsReliable ? string.Empty : "⚠";
    public string F6Display         => F6Score > 0 ? $"{F6Score:0.0}" : "–";
    public string F7Display         => F7Score > 0 ? $"{F7Score:0.0}" : "–";
    public string FilterDisplay     => IsFiltered ? $"⚠ {FilterReason}" : string.Empty;
    public string CorrDisplay       => double.IsNaN(BtcCorrelation) ? "–" : $"{BtcCorrelation:+0.00;-0.00}";
    public string DiversifiedDisplay => IsDiversifiedPick ? "★" : string.Empty;

    private static string Fmt(double p) => p switch
    {
        >= 1_000 => $"{p:#,0.00}",
        >= 1     => $"{p:F4}",
        >= 0.01  => $"{p:F6}",
        _        => $"{p:F8}",
    };
}
