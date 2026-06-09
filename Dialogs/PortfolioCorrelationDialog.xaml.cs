using System;
using System.Threading;
using System.Threading.Tasks;
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
/// Toont de correlatie van elke holding met BTC + een waarde-gewogen diversificatie-oordeel.
/// Draait de analyse asynchroon na openen (met voortgangsindicatie).
/// </summary>
public sealed partial class PortfolioCorrelationDialog : ContentDialog
{
    private readonly IPortfolioCorrelationService _service;
    private readonly Settings _settings;
    private readonly CancellationTokenSource _cts = new();

    private static readonly SolidColorBrush Green   = new(Color.FromArgb(255, 60, 179, 113));
    private static readonly SolidColorBrush Orange  = new(Color.FromArgb(255, 255, 167, 38));
    private static readonly SolidColorBrush Red     = new(Color.FromArgb(255, 205, 92, 92));
    private static readonly SolidColorBrush Neutral = new(Color.FromArgb(255, 160, 160, 160));

    public PortfolioCorrelationDialog(IPortfolioCorrelationService service, Settings settings)
    {
        _service  = service;
        _settings = settings;
        InitializeComponent();
        PrimaryButtonText = "Sluiten";
    }

    private void Dialog_Loading(FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _settings.AppTheme)
            sender.RequestedTheme = _settings.AppTheme;

        // Toon voortgang en start de analyse (fire-and-forget; UI-updates lopen op de UI-thread).
        ContentPanel.Children.Clear();
        var ring = new ProgressRing { IsActive = true, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Left };
        var status = new TextBlock { Text = "Analyseren…", FontSize = 12, Foreground = Neutral };
        ContentPanel.Children.Add(ring);
        ContentPanel.Children.Add(status);

        var progress = new Progress<(int done, int total, string status)>(p => status.Text = p.status);
        _ = RunAsync(progress);
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        _cts.Cancel();
    }

    private async Task RunAsync(IProgress<(int, int, string)> progress)
    {
        try
        {
            var result = await _service.AnalyzeAsync(progress, _cts.Token);
            if (!_cts.IsCancellationRequested)
                BuildContent(result);
        }
        catch (OperationCanceledException) { /* dialog gesloten */ }
        catch (Exception ex)
        {
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(new TextBlock
            {
                Text = $"Analyse mislukt: {ex.Message}",
                Foreground = Red, TextWrapping = TextWrapping.Wrap, FontSize = 12,
            });
        }
    }

    private void BuildContent(PortfolioCorrelationResult r)
    {
        ContentPanel.Children.Clear();

        if (r.TotalCount == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Geen holdings met waarde gevonden om te analyseren.",
                Foreground = Neutral, TextWrapping = TextWrapping.Wrap, FontSize = 13,
            });
            return;
        }

        // ── Kop: gewogen gemiddelde + verdict ─────────────────────────────────
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        var badge = new Border
        {
            CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10),
            Background = CorrBrush(r.WeightedAvgCorrelation), VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock
        {
            Text = double.IsNaN(r.WeightedAvgCorrelation) ? "—" : $"{r.WeightedAvgCorrelation:+0.00;-0.00}",
            FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White),
        };
        head.Children.Add(badge);
        var headText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        headText.Children.Add(new TextBlock { Text = "Gewogen correlatie met BTC", FontSize = 12, Foreground = Neutral });
        headText.Children.Add(new TextBlock { Text = r.Verdict, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
        headText.Children.Add(new TextBlock
        {
            Text = $"Hoog: {r.HighCount}  ·  Gemiddeld: {r.MediumCount}  ·  Laag: {r.LowCount}   ({r.AnalyzedCount}/{r.TotalCount} geanalyseerd)",
            FontSize = 11, Foreground = Neutral,
        });
        head.Children.Add(headText);
        ContentPanel.Children.Add(head);

        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Correlatie per holding (op 60 dagrendementen) — hoog = beweegt met BTC mee:",
            FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2),
        });

        // ── Per-coin rijen ────────────────────────────────────────────────────
        foreach (var c in r.Coins)
        {
            var grid = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            var name = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            name.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = c.Symbol, FontWeight = FontWeights.SemiBold });
            name.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "  " + c.Name, Foreground = Neutral });
            Grid.SetColumn(name, 0);

            // correlatiebalk
            var barBg = new Border
            {
                Height = 8, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            double frac = double.IsNaN(c.Correlation) ? 0 : Math.Max(0, Math.Min(1, Math.Abs(c.Correlation)));
            var bar = new Border
            {
                Height = 8, CornerRadius = new CornerRadius(4), Background = CorrBrush(c.Correlation),
                HorizontalAlignment = HorizontalAlignment.Left, Width = 100 * frac,
            };
            var barHost = new Grid { VerticalAlignment = VerticalAlignment.Center };
            barHost.Children.Add(barBg);
            barHost.Children.Add(bar);
            Grid.SetColumn(barHost, 1);

            var val = new TextBlock { Text = c.CorrelationDisplay, FontSize = 12, FontFamily = new FontFamily("Consolas"), Foreground = CorrBrush(c.Correlation), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 2);

            var value = new TextBlock { Text = c.ValueDisplay, FontSize = 11, Foreground = Neutral, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(value, 3);

            grid.Children.Add(name);
            grid.Children.Add(barHost);
            grid.Children.Add(val);
            grid.Children.Add(value);
            ContentPanel.Children.Add(grid);
        }

        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Tip: spreid over assets met lage onderlinge correlatie om concentratierisico te beperken. " +
                   "Coins zonder Binance-koersdata of met te weinig historie tonen '—'.",
            FontSize = 10, Foreground = Neutral, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        });
    }

    private static SolidColorBrush CorrBrush(double corr)
    {
        if (double.IsNaN(corr)) return Neutral;
        double c = Math.Abs(corr);
        return c >= 0.80 ? Red : c >= 0.50 ? Orange : Green;
    }
}
