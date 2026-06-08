using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Dialogs;

/// <summary>
/// Detail-popup voor één 3%-trading setup.
/// Content wordt programmatisch opgebouwd in <see cref="BuildContent"/>.
/// </summary>
public sealed partial class SetupDetailDialog : ContentDialog
{
    private readonly SetupDetailInfo _detail;
    private readonly Settings        _settings;

    // ── Brushes ──────────────────────────────────────────────────────────────
    private static readonly SolidColorBrush Green    = new(Color.FromArgb(255, 60, 179, 113));
    private static readonly SolidColorBrush Red      = new(Color.FromArgb(255, 205, 92,  92));
    private static readonly SolidColorBrush Orange   = new(Color.FromArgb(255, 255, 167, 38));
    private static readonly SolidColorBrush Neutral  = new(Color.FromArgb(255, 160, 160, 160));
    private static readonly SolidColorBrush Gold     = new(Color.FromArgb(255, 184, 134, 11));

    public SetupDetailDialog(SetupDetailInfo detail, Settings settings)
    {
        _detail   = detail;
        _settings = settings;
        InitializeComponent();
        Title = $"{detail.Row.CoinName}  ({detail.Row.Symbol})  —  {detail.Row.Bias}";
        PrimaryButtonText   = "Sluiten";
        IsPrimaryButtonEnabled = true;
    }

    private void Dialog_Loading(FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _settings.AppTheme)
            sender.RequestedTheme = _settings.AppTheme;
        BuildContent();
    }

    // =========================================================================
    // Content builder
    // =========================================================================

    private void BuildContent()
    {
        var r = _detail;
        ContentPanel.Children.Clear();

        // ── 1. Trade-levels samenvatting ──────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("📊 Trade-niveaus"));
        var levelsGrid = MakeGrid(new[] { "*", "*", "*", "*" });
        AddCard(levelsGrid, 0, "Entry",  Fmt(r.Row.EntryPrice), null);
        AddCard(levelsGrid, 1, "Stop Loss", Fmt(r.Row.StopLoss), Red);
        AddCard(levelsGrid, 2, "TP (+3% netto)", Fmt(r.Row.TakeProfit), Green);
        AddCard(levelsGrid, 3, "R/R",    r.Row.RRDisplay, r.Row.RiskReward >= 1.5 ? Green : Orange);
        ContentPanel.Children.Add(levelsGrid);

        // ── 2. Factorscores ───────────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("🎯 Factorscores (5-factor model)"));
        var factorText = new TextBlock
        {
            Text        = r.Row.FactorBreakdown,
            FontFamily  = new FontFamily("Consolas"),
            FontSize    = 13,
            TextWrapping = TextWrapping.Wrap,
        };
        ContentPanel.Children.Add(factorText);

        // Score-indicator: klasse + historische hitrate
        var scoreRow = HRow(
            Label("Scoreklasse:", 12),
            Value($"{r.Row.ScoreClass}", 13, null, bold: true),
            Label("  Hist. hitrate:", 12),
            Value(r.Row.HitrateDisplay, 13, r.Row.HistHitrate >= 55 ? Green : r.Row.HistHitrate >= 45 ? Neutral : Red, bold: true),
            Label("  Expectancy:", 12),
            Value(r.Row.ExpectancyDisplay, 13, r.Row.Expectancy >= 0 ? Green : Red, bold: true));
        ContentPanel.Children.Add(scoreRow);

        // ── 3. Kernindicatoren ────────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("📈 Kernindicatoren"));
        var indGrid = MakeGrid(new[] { "*", "*", "*" });

        SolidColorBrush rsiColor = _detail.Rsi < 30 ? Green
                                 : _detail.Rsi > 70 ? Red : Neutral;
        AddKv(indGrid, 0, "RSI (14)",      r.RsiDisplay,  rsiColor);
        AddKv(indGrid, 1, "MACD histogram", $"{r.MacdHistogram:+0.0000;-0.0000}", r.MacdHistogram > 0 ? Green : Red);
        AddKv(indGrid, 2, "ATR (% prijs)", r.AtrDisplay,  Neutral);

        ContentPanel.Children.Add(indGrid);

        var emaPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
        emaPanel.Children.Add(SubLabel("EMA-context:"));
        emaPanel.Children.Add(new TextBlock
        {
            Text     = r.EmaStatusDisplay,
            FontSize = 12,
            Foreground = r.EmaContext.Contains("Golden") ? Green
                       : r.EmaContext.Contains("Death")  ? Red  : Neutral,
            TextWrapping = TextWrapping.Wrap,
        });
        if (r.IsSqueeze)
        {
            emaPanel.Children.Add(new TextBlock
            {
                Text     = "🔥 Squeeze actief — breakout verwacht (richting onduidelijk)",
                FontSize = 12,
                Foreground = Orange,
            });
        }
        emaPanel.Children.Add(new TextBlock
        {
            Text     = $"Volume t.o.v. 20-bar gem.: {r.VolumeRatioPct:0}%",
            FontSize = 12,
            Foreground = r.VolumeRatioPct > 150 ? Green : r.VolumeRatioPct < 70 ? Red : Neutral,
        });
        ContentPanel.Children.Add(emaPanel);

        // ── 4. Support & Resistance ───────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("📐 Support & Resistance (60-bar pivots)"));
        var srPanel = new StackPanel { Spacing = 4 };
        if (r.NearResistances.Count > 0)
            srPanel.Children.Add(new TextBlock
            {
                Text     = "Weerstand: " + string.Join(" | ", r.NearResistances.Select(Fmt)),
                FontSize = 12,
                Foreground = Red,
            });
        if (r.NearSupports.Count > 0)
            srPanel.Children.Add(new TextBlock
            {
                Text     = "Steun:      " + string.Join(" | ", r.NearSupports.Select(Fmt)),
                FontSize = 12,
                Foreground = Green,
            });
        if (r.NearResistances.Count == 0 && r.NearSupports.Count == 0)
            srPanel.Children.Add(new TextBlock { Text = "Geen duidelijke pivots gevonden in 60 bars.", FontSize = 12, Foreground = Neutral });
        ContentPanel.Children.Add(srPanel);

        // ── 5. BTC-correlatie ─────────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("🔗 Correlatie met BTC (60 dagrendementen)"));
        var corrColor = Math.Abs(r.BtcCorrelation) >= 0.80 ? Orange : Neutral;
        var corrLine  = new TextBlock
        {
            Text       = r.CorrelationDisplay,
            FontSize   = 13,
            FontFamily = new FontFamily("Consolas"),
            Foreground = corrColor,
        };
        ContentPanel.Children.Add(corrLine);
        if (r.IsHighCorrelation)
            ContentPanel.Children.Add(new TextBlock
            {
                Text       = "⚠ Hoge BTC-correlatie — in diversificatiecontext telt dit als 1 BTC-long, niet als zelfstandige setup.",
                FontSize   = 11,
                Foreground = Orange,
                TextWrapping = TextWrapping.Wrap,
            });

        // ── 6. Positionering (F7) ─────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("📉 Positionering (Binance Futures)"));
        var posGrid = MakeGrid(new[] { "*", "*", "*" });

        SolidColorBrush fundColor = Neutral;
        if (r.FundingRatePct.HasValue)
        {
            double fr = r.FundingRatePct.Value;
            fundColor = fr < -0.03 ? Green : fr > 0.1 ? Red : Neutral;
        }
        AddKv(posGrid, 0, "Funding rate", r.FundingDisplay, fundColor);

        SolidColorBrush lsColor = Neutral;
        if (r.LongShortRatio.HasValue)
            lsColor = r.LongShortRatio < 0.8 ? Green : r.LongShortRatio > 2.0 ? Red : Neutral;
        AddKv(posGrid, 1, "Long/Short ratio", r.LSDisplay, lsColor);
        AddKv(posGrid, 2, "Open interest", r.OpenInterest.HasValue ? $"{r.OpenInterest:N0}" : "n/v", Neutral);
        ContentPanel.Children.Add(posGrid);

        // ── 7. Liquiditeit (F6) ────────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("💧 Liquiditeit (Binance order book)"));
        var liqGrid = MakeGrid(new[] { "*", "*" });
        SolidColorBrush spreadColor = r.BidAskSpreadPct.HasValue
            ? (r.BidAskSpreadPct < 0.05 ? Green : r.BidAskSpreadPct > 0.20 ? Red : Neutral)
            : Neutral;
        AddKv(liqGrid, 0, "Bid-ask spread", r.SpreadDisplay, spreadColor);
        SolidColorBrush depthColor = r.MinDepthUsdt.HasValue
            ? (r.MinDepthUsdt > 100_000 ? Green : r.MinDepthUsdt < 20_000 ? Red : Neutral)
            : Neutral;
        AddKv(liqGrid, 1, "Min. orderdiepte", r.DepthDisplay, depthColor);
        ContentPanel.Children.Add(liqGrid);

        // Filter-status
        if (r.Row.IsFiltered)
            ContentPanel.Children.Add(new InfoBar
            {
                IsOpen    = true,
                IsClosable = false,
                Severity  = InfoBarSeverity.Warning,
                Title     = "Gefilterd",
                Message   = r.Row.FilterReason,
            });

        // ── 8. Invalidatieniveau ───────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("🚫 Invalidatie"));
        ContentPanel.Children.Add(new TextBlock
        {
            Text        = r.InvalidationNote,
            FontSize    = 13,
            Foreground  = Red,
            TextWrapping = TextWrapping.Wrap,
        });

        // ── 9. Macro-events ────────────────────────────────────────────────────
        ContentPanel.Children.Add(SectionHeader("📅 Macro-events (komende 15 dagen)"));
        if (r.UpcomingEvents.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Geen bekende macro-events in de komende 15 dagen.",
                FontSize = 12,
                Foreground = Neutral,
            });
        }
        else
        {
            foreach (var ev in r.UpcomingEvents)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text     = $"⚠  {ev.ShortDisplay}",
                    FontSize = 12,
                    Foreground = Orange,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }

        // ── 10. Disclaimer ────────────────────────────────────────────────────
        ContentPanel.Children.Add(new TextBlock
        {
            Text = $"Gebouwd: {r.BuiltAt:dd-MM-yyyy HH:mm}  |  Scores en hitrates zijn modeluitkomsten, geen garanties. " +
                   "CPI/PCE-datums zijn benaderingen — check BLS.gov voor exacte data.",
            FontSize    = 10,
            Foreground  = Neutral,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        });
    }

    // =========================================================================
    // UI helpers
    // =========================================================================

    private static TextBlock SectionHeader(string text) => new()
    {
        Text       = text,
        FontSize   = 14,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 6, 0, 2),
    };

    private static TextBlock Label(string text, double size = 12) => new()
    {
        Text       = text,
        FontSize   = size,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 160, 160, 160)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock SubLabel(string text) => new()
    {
        Text       = text,
        FontSize   = 11,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 140, 140, 140)),
        FontWeight = FontWeights.SemiBold,
    };

    private static TextBlock Value(string text, double size, SolidColorBrush? color, bool bold = false) => new()
    {
        Text       = text,
        FontSize   = size,
        FontFamily = new FontFamily("Consolas"),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = color ?? new SolidColorBrush(Colors.White),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static StackPanel HRow(params UIElement[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var item in items) sp.Children.Add(item);
        return sp;
    }

    private static Grid MakeGrid(string[] columnWidths)
    {
        var g = new Grid { ColumnSpacing = 10 };
        foreach (var w in columnWidths)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = w == "*"
                    ? new GridLength(1, GridUnitType.Star)
                    : GridLength.Auto,
            });
        }
        return g;
    }

    private static void AddCard(Grid grid, int col, string label, string val, SolidColorBrush? color)
    {
        var border = new Border
        {
            Background   = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(10, 8, 10, 8),
        };
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Neutral });
        sp.Children.Add(new TextBlock
        {
            Text       = val,
            FontSize   = 14,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = color ?? new SolidColorBrush(Colors.White),
        });
        border.Child = sp;
        Grid.SetColumn(border, col);
        grid.Children.Add(border);
    }

    private static void AddKv(Grid grid, int col, string label, string val, SolidColorBrush? color)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Neutral });
        sp.Children.Add(new TextBlock
        {
            Text       = val,
            FontSize   = 13,
            FontFamily = new FontFamily("Consolas"),
            Foreground = color ?? new SolidColorBrush(Colors.White),
        });
        Grid.SetColumn(sp, col);
        grid.Children.Add(sp);
    }

    private static string Fmt(double p) => p switch
    {
        >= 1_000 => $"{p:#,0.00}",
        >= 1     => $"{p:F4}",
        >= 0.01  => $"{p:F6}",
        _        => $"{p:F8}",
    };
}
