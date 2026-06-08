using System.ComponentModel.DataAnnotations.Schema;
using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// A snapshot of a pattern trade setup the user chose to follow.
/// Stored in SQLite; status is updated automatically when AnalyzePortfolio
/// detects that the current price has crossed TP1 or the stop-loss.
/// </summary>
public class WatchedSetup
{
    public int    Id             { get; set; }

    // ── Coin identity (denormalised — no FK; coin may not be in portfolio) ──
    public string CoinApiId      { get; set; } = string.Empty;
    public string CoinName       { get; set; } = string.Empty;
    public string CoinSymbol     { get; set; } = string.Empty;
    public string ImageUri       { get; set; } = string.Empty;

    // ── Setup snapshot ────────────────────────────────────────────────────────
    public string Direction      { get; set; } = "Long";   // "Long" / "Short"
    public double EntryPrice     { get; set; }
    public double StopLoss       { get; set; }
    public double Target1        { get; set; }
    public double Target2        { get; set; }
    public int    Score          { get; set; }
    public string PatternSummary { get; set; } = string.Empty;  // "Bull Flag 1D · Bijna Breakout 4H"
    public string Bias1D         { get; set; } = string.Empty;
    public string Bias4H         { get; set; } = string.Empty;

    // ── Strategy context (captured at creation time) ─────────────────────────

    /// <summary>BTC market regime snapshot when the setup was added ("RiskOn" | "Neutral" | "RiskOff").</summary>
    public string? MarketRegimeAtCreation { get; set; }

    /// <summary>True when TP2 has been hit (auto-detected during price check).</summary>
    public bool Tp2Hit { get; set; }

    /// <summary>Optional FK to the ExchangeOrder that resulted from this setup.</summary>
    public int? LinkedOrderId { get; set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    public DateTime          AddedAt    { get; set; } = DateTime.UtcNow;
    public WatchedSetupStatus Status    { get; set; } = WatchedSetupStatus.Watching;

    /// <summary>Timestamp when entry was detected (price crossed the entry level). Null when not yet in trade.</summary>
    public DateTime? EntryAt    { get; set; }

    /// <summary>Price at which the outcome was determined (at TP1/SL hit).</summary>
    public double?   ClosePrice { get; set; }
    public DateTime? ClosedAt   { get; set; }

    // ── Runtime-only (not stored) ────────────────────────────────────────────

    /// <summary>Live market price — populated by SetupTrackerViewModel at refresh, not saved to DB.</summary>
    [NotMapped]
    public double CurrentPrice { get; set; }

    // ── Computed helpers (not stored) ─────────────────────────────────────────

    /// <summary>R/R ratio: reward (TP1–Entry) / risk (Entry–SL). 0 when levels are missing.</summary>
    public double RiskReward =>
        StopLoss > 0 && Target1 > 0 && EntryPrice > 0
            ? Math.Abs(Target1 - EntryPrice) / Math.Abs(EntryPrice - StopLoss)
            : 0;

    /// <summary>Profit/loss % at close relative to entry price. Null when not closed.</summary>
    public double? PnlPct =>
        ClosePrice.HasValue && EntryPrice > 0
            ? (Direction == "Short"
                ? (EntryPrice - ClosePrice.Value) / EntryPrice * 100
                : (ClosePrice.Value - EntryPrice) / EntryPrice * 100)
            : null;

    /// <summary>
    /// Live unrealised P&amp;L % for Open trades: current price vs entry.
    /// Positive = in profit direction, Negative = in loss direction.
    /// Null when status is not Open, or CurrentPrice is unknown.
    /// </summary>
    public double? UnrealisedPnlPct
    {
        get
        {
            if (Status != WatchedSetupStatus.Open) return null;
            if (CurrentPrice <= 0 || EntryPrice <= 0) return null;
            return Direction == "Short"
                ? (EntryPrice - CurrentPrice) / EntryPrice * 100
                : (CurrentPrice - EntryPrice) / EntryPrice * 100;
        }
    }

    /// <summary>
    /// Distance of the current live price from the entry level.
    /// Positive = moving in the profitable direction (above entry for Long, below for Short).
    /// NaN when CurrentPrice is unknown.
    /// </summary>
    public double EntryDistancePct =>
        CurrentPrice > 0 && EntryPrice > 0
            ? (Direction == "Short"
                ? (EntryPrice - CurrentPrice) / EntryPrice * 100
                : (CurrentPrice - EntryPrice) / EntryPrice * 100)
            : double.NaN;
}
