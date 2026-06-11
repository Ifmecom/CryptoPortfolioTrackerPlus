using CryptoPortfolioTracker.Converters;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Helpers;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.ViewModels;

/// <summary>
/// Lightweight display model for a single coin in the Pattern Trading list.
/// Wraps <see cref="PatternCoinAnalysis"/> and exposes pre-computed XAML-friendly properties.
/// </summary>
public class PatternCoinRow
{
    // ── Source data (kept for drill-down / share) ───────────────────────────
    public PatternCoinAnalysis Analysis { get; }

    // ── Identity ────────────────────────────────────────────────────────────
    public string Name        => Analysis.Coin.Name;
    public string Symbol      => Analysis.Coin.Symbol?.ToUpperInvariant() ?? "";
    public string ImageUri    => Analysis.Coin.ImageUri;
    public bool   HasHolding  => Analysis.HasHolding;
    public bool   IsWatchlist => Analysis.IsWatchlist;

    /// <summary>ApiId exposed so the ViewModel can call RemoveFromWatchlist.</summary>
    public string ApiId => Analysis.Coin.ApiId ?? string.Empty;

    public Microsoft.UI.Xaml.Media.ImageSource? Image =>
        Functions.StringToImageSource(ImageUri);

    // ── Market cap rank ─────────────────────────────────────────────────────
    public long       Rank        => Analysis.Coin.Rank;
    public string     RankDisplay => Rank > 0 ? $"#{Rank}" : "";
    public Visibility RankVis     => Rank > 0 ? Visibility.Visible : Visibility.Collapsed;

    // ── Market data ─────────────────────────────────────────────────────────
    public string PriceDisplay  => FormatPrice(Analysis.Coin.Price);
    public string Change24h     => $"{Analysis.Coin.Change24Hr:+0.00;-0.00;0.00}%";
    public string MarketCap     => FormatLargeNumber(Analysis.Coin.MarketCap);

    public SolidColorBrush Change24hBrush =>
        AnalysisHelpers.SignedColor(Analysis.Coin.Change24Hr);

    // ── Score ───────────────────────────────────────────────────────────────
    public int    Score      => Analysis.TradabilityScore;
    public string ScoreText  => Score.ToString();
    public string ScoreLabel => Analysis.ScoreLabel;

    public SolidColorBrush ScoreBrush => Score switch
    {
        >= 80 => new SolidColorBrush(Color.FromArgb(0xFF, 0x27, 0x96, 0x42)), // deep green
        >= 60 => new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0xC0, 0x50)), // green
        >= 40 => new SolidColorBrush(Color.FromArgb(0xFF, 0xE6, 0x7E, 0x22)), // orange
        _     => new SolidColorBrush(Color.FromArgb(0xFF, 0x99, 0x99, 0x99)), // grey
    };

    // ── Direction ───────────────────────────────────────────────────────────
    public string             Direction      => Analysis.PrimaryDirection;
    public SolidColorBrush    DirectionBrush => AnalysisHelpers.DirectionColor(Direction);

    // ── Timeframe bias badges ───────────────────────────────────────────────
    public string DailyBias => Analysis.DailyBias;
    public string H4Bias    => Analysis.H4Bias;
    public string H1Bias    => Analysis.H1Bias;
    public string M15Bias   => Analysis.M15Bias;
    public string DailyRsi  => Analysis.DailyRsi > 0 ? $"RSI {Analysis.DailyRsi:F0}" : "";
    public string H4Rsi     => Analysis.H4Rsi    > 0 ? $"RSI {Analysis.H4Rsi:F0}"    : "";
    public string H1Rsi     => Analysis.H1Rsi    > 0 ? $"RSI {Analysis.H1Rsi:F0}"    : "";
    public string M15Rsi    => Analysis.M15Rsi   > 0 ? $"RSI {Analysis.M15Rsi:F0}"   : "";

    public SolidColorBrush DailyBiasBrush => BiasBrush(DailyBias);
    public SolidColorBrush H4BiasBrush    => BiasBrush(H4Bias);
    public SolidColorBrush H1BiasBrush    => BiasBrush(H1Bias);
    public SolidColorBrush M15BiasBrush   => BiasBrush(M15Bias);

    // ── TF conflict warning ──────────────────────────────────────────────────
    /// <summary>True when daily and 4H biases disagree — cautionary signal.</summary>
    public bool   HasTfConflict    => !string.IsNullOrEmpty(DailyBias) && !string.IsNullOrEmpty(H4Bias)
                                      && DailyBias != "Neutraal" && H4Bias != "Neutraal"
                                      && DailyBias != H4Bias;
    public string TfConflictText   => HasTfConflict ? "⚠ TF-conflict 1D/4H" : "";
    public Visibility TfConflictVis => HasTfConflict ? Visibility.Visible : Visibility.Collapsed;

    // ── Patterns ────────────────────────────────────────────────────────────
    /// <summary>Top-6 strongest patterns, used as badge chips in the card.</summary>
    public IReadOnlyList<PatternBadge> PatternBadges { get; }

    /// <summary>Number of qualifying patterns beyond the first 6 (for "+N" overflow chip).</summary>
    public int        OverflowCount  { get; }
    public bool       HasOverflow    => OverflowCount > 0;
    public string     OverflowText   => $"+{OverflowCount}";
    public Visibility OverflowVis    => HasOverflow ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Multi-line overzicht van de patronen áchter de "+N"-chip (voor de mouseover-tooltip).</summary>
    public string     OverflowToolTip { get; }

    // ── Near-breakout indicator ─────────────────────────────────────────────
    public bool   IsNearBreakout    => Analysis.IsNearBreakout;
    public string BreakoutIndicator => IsNearBreakout ? "⚡ Bijna Breakout" : "";

    // ── Setup ───────────────────────────────────────────────────────────────
    public bool HasSetup => Analysis.Setup is not null && Analysis.TradabilityScore >= 40;

    public string EntryDisplay   => HasSetup ? FormatPrice(Analysis.Setup!.EntryPrice)  : "–";
    public string StopDisplay    => HasSetup ? FormatPrice(Analysis.Setup!.StopLoss)     : "–";
    public string Target1Display => HasSetup ? FormatPrice(Analysis.Setup!.Target1)      : "–";
    public string Target2Display => HasSetup ? FormatPrice(Analysis.Setup!.Target2)      : "–";
    public string RRDisplay      => HasSetup ? $"1:{Analysis.Setup!.RiskReward1:F1}"    : "–";
    public string ConfidenceText => HasSetup ? Analysis.Setup!.Confidence               : "";
    public string EntryNote      => HasSetup ? Analysis.Setup!.EntryNote                : "";

    /// <summary>Key bullet points (max 4) explaining the setup rationale.</summary>
    public IReadOnlyList<string> ReasoningBullets { get; }

    // ── Data quality ────────────────────────────────────────────────────────
    public string     DataSource        => Analysis.DataSource;
    public bool       HasData           => Analysis.HasData;
    public bool       ShowNoData        => !HasData;

    // ── Staleness indicator ──────────────────────────────────────────────────
    public bool   IsStale        => (DateTime.Now - Analysis.AnalyzedAt).TotalHours > 1;
    public string StalenessText  => IsStale
        ? $"⚠ {(int)(DateTime.Now - Analysis.AnalyzedAt).TotalMinutes}m geleden"
        : $"{(int)(DateTime.Now - Analysis.AnalyzedAt).TotalMinutes}m geleden";
    public SolidColorBrush StalenessColor => IsStale
        ? new SolidColorBrush(Color.FromArgb(0xFF, 0xE6, 0x7E, 0x22))   // orange
        : new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));  // grey

    // ── Visibility helpers (used directly in DataTemplate x:Bind) ───────────
    public Visibility HasDataVis        => HasData     ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasSetupVis       => HasSetup    ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowNoDataVis     => ShowNoData  ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WatchlistBadgeVis => IsWatchlist ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HoldingBadgeVis   => HasHolding  ? Visibility.Visible : Visibility.Collapsed;

    public string AnalyzedAtText => Analysis.AnalyzedAt.ToString("HH:mm");

    // ── Fundamental quality (set by ViewModel uit IFundamentalsService) ───────
    public double FundamentalScore   { get; set; }
    public string FundamentalVerdict { get; set; } = string.Empty;
    public bool   HasFundamental     { get; set; }
    public string FundamentalDisplay =>
        HasFundamental ? $"Ⓕ {FundamentalScore:0} · {FundamentalVerdict}" : string.Empty;
    public Visibility FundamentalVis => HasFundamental ? Visibility.Visible : Visibility.Collapsed;

    // ── #5: on-demand liquiditeit (F6) — gezet door de ViewModel na een check ──
    public bool LiquidityChecked { get; set; }
    public LiquidityClassifier.Level LiquidityLevel { get; set; } = LiquidityClassifier.Level.Unknown;
    public string LiquidityDisplay => LiquidityClassifier.Label(LiquidityLevel);
    public Visibility LiquidityVis => LiquidityChecked ? Visibility.Visible : Visibility.Collapsed;
    public SolidColorBrush LiquidityBrush => LiquidityLevel switch
    {
        LiquidityClassifier.Level.Good   => new SolidColorBrush(Color.FromArgb(0xFF, 0x27, 0x96, 0x42)),
        LiquidityClassifier.Level.Medium => new SolidColorBrush(Color.FromArgb(0xFF, 0xE6, 0x7E, 0x22)),
        LiquidityClassifier.Level.Thin   => new SolidColorBrush(Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)),
        _                                => new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x80)),
    };

    // ── Constructor ─────────────────────────────────────────────────────────
    public PatternCoinRow(PatternCoinAnalysis analysis)
    {
        Analysis = analysis;

        // Build pattern badges — max 6, sorted by strength desc, bullish before bearish
        var qualifying = analysis.Patterns
            .Where(p => p.Strength >= 55)
            .OrderByDescending(p => p.Category == PatternCategory.Bullish ? 1 : p.Category == PatternCategory.Bearish ? -1 : 0)
            .ThenByDescending(p => p.Strength)
            .ToList();

        PatternBadges  = qualifying.Take(6).Select(p => new PatternBadge(p, analysis)).ToList();

        // Overflow: de patronen die niet als badge passen — opgesomd in de mouseover-tooltip.
        var overflow   = qualifying.Skip(6).ToList();
        OverflowCount  = overflow.Count;
        OverflowToolTip = overflow.Count == 0
            ? string.Empty
            : "Overige patronen:\n" + string.Join("\n", overflow.Select(p =>
                $"• {p.Timeframe,-3} {p.DisplayName} · sterkte {p.Strength}{(p.IsConfirmed ? " ✓ bevestigd" : "")}"));

        // Setup reasoning — max 4 bullets
        ReasoningBullets = analysis.Setup?.Reasoning.Take(4).ToList()
                           ?? new List<string>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string FormatPrice(double price) => price switch
    {
        >= 10_000 => $"${price:N0}",
        >= 1      => $"${price:N2}",
        >= 0.01   => $"${price:N4}",
        _         => $"${price:N6}",
    };

    private static string FormatLargeNumber(double value) => value switch
    {
        >= 1e12 => $"${value / 1e12:F1}T",
        >= 1e9  => $"${value / 1e9:F1}B",
        >= 1e6  => $"${value / 1e6:F1}M",
        _       => $"${value:N0}",
    };

    private static SolidColorBrush BiasBrush(string bias) => bias switch
    {
        "Bullish" => new SolidColorBrush(Color.FromArgb(0xFF, 0x27, 0x96, 0x42)),
        "Bearish" => new SolidColorBrush(Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)),
        _         => new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x80)),
    };
}

/// <summary>Chip badge representing a single detected pattern.</summary>
public class PatternBadge
{
    public string              Label      { get; }
    public string              Timeframe  { get; }
    public SolidColorBrush     Background { get; }
    public string              ToolTip    { get; }
    public bool                Confirmed  { get; }

    /// <summary>Underlying pattern — passed to <see cref="Views.CoinChartWindow"/> for annotation.</summary>
    public PatternResult       Pattern    { get; }

    /// <summary>Parent analysis — needed to open the chart window with the correct bars.</summary>
    public PatternCoinAnalysis Analysis   { get; }

    public PatternBadge(PatternResult pattern, PatternCoinAnalysis analysis)
    {
        Pattern   = pattern;
        Analysis  = analysis;
        Label     = pattern.DisplayName;
        Timeframe = pattern.Timeframe;
        ToolTip   = pattern.Description;
        Confirmed = pattern.IsConfirmed;

        Background = pattern.Category switch
        {
            PatternCategory.Bullish => new SolidColorBrush(Color.FromArgb(0xFF, 0x27, 0x96, 0x42)),
            PatternCategory.Bearish => new SolidColorBrush(Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)),
            PatternCategory.Warning => new SolidColorBrush(Color.FromArgb(0xFF, 0xE6, 0x7E, 0x22)),
            _                       => new SolidColorBrush(Color.FromArgb(0xFF, 0x60, 0x60, 0x80)),
        };
    }
}
