using System;
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
/// Portfolio-breed risico-overzicht: open risico, blootstelling, dag-P&amp;L en guardrail-status.
/// </summary>
public sealed partial class RiskDashboardDialog : ContentDialog
{
    private readonly IRiskDashboardService _service;
    private readonly Settings _settings;

    private static readonly SolidColorBrush Green   = new(Color.FromArgb(255, 60, 179, 113));
    private static readonly SolidColorBrush Orange  = new(Color.FromArgb(255, 255, 167, 38));
    private static readonly SolidColorBrush Red     = new(Color.FromArgb(255, 205, 92, 92));
    private static readonly SolidColorBrush Neutral = new(Color.FromArgb(255, 160, 160, 160));

    public RiskDashboardDialog(IRiskDashboardService service, Settings settings)
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

        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new ProgressRing { IsActive = true, Width = 26, Height = 26, HorizontalAlignment = HorizontalAlignment.Left });
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            var r = await _service.BuildAsync();
            Build(r);
        }
        catch (Exception ex)
        {
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(new TextBlock { Text = $"Kon risico niet berekenen: {ex.Message}", Foreground = Red, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        }
    }

    private void Build(RiskDashboard r)
    {
        ContentPanel.Children.Clear();

        // ── Statusbanner ───────────────────────────────────────────────────────
        var (statusText, statusBrush) = r.Status switch
        {
            RiskSeverity.Critical => ("Grens bereikt", Red),
            RiskSeverity.Warning  => ("Let op", Orange),
            _                     => ("OK", Green),
        };
        var banner = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Background = statusBrush };
        banner.Child = new TextBlock { Text = $"Risico-status: {statusText}", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        ContentPanel.Children.Add(banner);
        ContentPanel.Children.Add(new TextBlock
        {
            Text = $"Berekend t.o.v. {r.CapitalBasis}: {r.Capital:#,0} USDT.",
            FontSize = 10, Foreground = Neutral,
        });

        // ── Kerncijfers ────────────────────────────────────────────────────────
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddMetric(grid, 0, 0, "Open posities", $"{r.OpenPositions}" + (r.MaxOpenPositions > 0 ? $" / {r.MaxOpenPositions}" : ""),
                  r.MaxOpenPositions > 0 && r.OpenPositions >= r.MaxOpenPositions ? Orange : null);
        AddMetric(grid, 0, 1, "Totaal open risico", $"{r.TotalOpenRiskUsd:#,0} USDT  ({r.OpenRiskPct:0.0}%)",
                  r.OpenRiskPct > RiskDashboardCalculator.TotalHeatWarnPct ? Orange : null);
        AddMetric(grid, 0, 2, "Grootste positie-risico", $"{r.LargestPositionRiskPct:0.0}%",
                  _settings.MaxPortfolioPercPerTrade > 0 && r.LargestPositionRiskPct > _settings.MaxPortfolioPercPerTrade + 0.05 ? Orange : null);
        AddMetric(grid, 1, 0, "Blootstelling", $"{r.ExposureUsd:#,0} USDT  ({r.ExposurePct:0.0}%)", null);
        AddMetric(grid, 1, 1, "Dag-P&L", $"{r.DayRealizedPnlUsd:+#,0.00;-#,0.00} USDT",
                  r.DayRealizedPnlUsd < 0 ? Red : r.DayRealizedPnlUsd > 0 ? Green : Neutral);
        AddMetric(grid, 1, 2, "Verlieslimiet (dag)", r.DailyLossLimitUsd > 0 ? $"{r.DailyLossLimitUsd:#,0} USDT" : "—",
                  r.DailyLossLimitUsd > 0 && r.DayRealizedPnlUsd <= -r.DailyLossLimitUsd ? Red : null);
        ContentPanel.Children.Add(grid);

        // ── Alerts ───────────────────────────────────────────────────────────────
        ContentPanel.Children.Add(new TextBlock { Text = "Guardrails", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) });
        if (r.Alerts.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock { Text = "✅ Geen waarschuwingen — je zit binnen al je ingestelde grenzen.", FontSize = 12, Foreground = Green });
        }
        else
        {
            foreach (var a in r.Alerts)
                ContentPanel.Children.Add(new InfoBar
                {
                    IsOpen = true, IsClosable = false,
                    Severity = a.Severity == RiskSeverity.Critical ? InfoBarSeverity.Error : InfoBarSeverity.Warning,
                    Message = a.Message,
                });
        }

        ContentPanel.Children.Add(new TextBlock
        {
            Text = "Grenzen pas je aan in Instellingen → Risk-guardrails. 'Totaal open risico' = som van (verlies bij SL) over alle open posities.",
            FontSize = 10, Foreground = Neutral, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        });
    }

    private static void AddMetric(Grid grid, int row, int col, string label, string value, SolidColorBrush? valueBrush)
    {
        var sp = new StackPanel { Spacing = 1 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Neutral });
        sp.Children.Add(new TextBlock
        {
            Text = value, FontSize = 15, FontWeight = FontWeights.SemiBold,
            Foreground = valueBrush ?? (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"],
        });
        Grid.SetRow(sp, row);
        Grid.SetColumn(sp, col);
        grid.Children.Add(sp);
    }
}
