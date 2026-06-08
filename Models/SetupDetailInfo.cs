using CryptoPortfolioTracker.Services;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Volledige details van één setup voor het detailvenster in de 3%-trading-tool.
/// Wordt on-the-fly berekend via ThreePctScoringService.BuildDetailInfo().
/// </summary>
public sealed class SetupDetailInfo
{
    // ── Basisinformatie ───────────────────────────────────────────────────────
    public ThreePctLiveRow Row     { get; init; } = null!;
    public string          Bias   { get; init; } = "Long";
    public DateTime        BuiltAt { get; init; } = DateTime.Now;

    // ── Factor-scores met uitleg ──────────────────────────────────────────────
    public double F1Trend      { get; init; }
    public double F2Momentum   { get; init; }
    public double F3Volume     { get; init; }
    public double F4Volatility { get; init; }
    public double F5SR         { get; init; }

    // ── Kernindicatoren ───────────────────────────────────────────────────────
    public double Rsi            { get; init; }
    public double MacdHistogram  { get; init; }
    public double Ema50          { get; init; }
    public double Ema200         { get; init; }
    public double Ema50DistPct   { get; init; }
    public double Ema200DistPct  { get; init; }
    public double AtrPct         { get; init; }
    public bool   IsSqueeze      { get; init; }
    public string EmaContext     { get; init; } = "–";   // "Golden Cross" / "Death Cross" etc.
    public double VolumeRatioPct { get; init; }          // huidige vol / 20-bar gem als %

    // ── Support & Resistance ─────────────────────────────────────────────────
    public IReadOnlyList<double> NearSupports     { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> NearResistances  { get; init; } = Array.Empty<double>();

    // ── BTC-correlatie ────────────────────────────────────────────────────────
    public double BtcCorrelation    { get; init; }
    public string CorrelationLabel  { get; init; } = "–";
    public bool   IsHighCorrelation => Math.Abs(BtcCorrelation) >= 0.80;

    // ── F6: Liquiditeit ───────────────────────────────────────────────────────
    public double? BidAskSpreadPct  { get; init; }
    public double? MinDepthUsdt     { get; init; }

    // ── F7: Positionering ─────────────────────────────────────────────────────
    public double? FundingRatePct   { get; init; }
    public double? LongShortRatio   { get; init; }
    public double? OpenInterest     { get; init; }

    // ── Invalidatieniveau ─────────────────────────────────────────────────────
    /// <summary>Concreet prijsniveau of omschrijving waarop de setup ongeldig wordt.</summary>
    public string InvalidationNote  { get; init; } = string.Empty;

    // ── Macro-events ──────────────────────────────────────────────────────────
    public IReadOnlyList<MacroEvent> UpcomingEvents { get; init; } = Array.Empty<MacroEvent>();

    // ── Display helpers ───────────────────────────────────────────────────────

    public string RsiDisplay  => Rsi > 0 ? $"{Rsi:0.0}" : "–";
    public string AtrDisplay  => AtrPct > 0 ? $"{AtrPct:0.00}%" : "–";

    public string FundingDisplay => FundingRatePct.HasValue
        ? $"{FundingRatePct:+0.000;-0.000}%"
        : "n/v (spot)";

    public string LSDisplay => LongShortRatio.HasValue
        ? $"{LongShortRatio:F2}"
        : "n/v (spot)";

    public string SpreadDisplay => BidAskSpreadPct.HasValue
        ? $"{BidAskSpreadPct:0.000}%"
        : "–";

    public string DepthDisplay => MinDepthUsdt.HasValue
        ? $"${MinDepthUsdt:N0}"
        : "–";

    public string CorrelationDisplay =>
        $"{BtcCorrelation:+0.00;-0.00}  ({CorrelationLabel})";

    public string EmaStatusDisplay =>
        $"{EmaContext}  (EMA50 {Ema50DistPct:+0.0;-0.0}%,  EMA200 {Ema200DistPct:+0.0;-0.0}%)";
}
