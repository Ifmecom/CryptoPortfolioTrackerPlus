using System;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Controls;

/// <summary>
/// Zelfstandige, herbruikbare balk met de gedeelde marktcontext (regime + Fear &amp; Greed +
/// eerstvolgende macro-event). Laadt zichzelf via <see cref="IMarketContextService"/> (gecached),
/// dus elke tab kan 'm tonen met enkel <c>&lt;controls:MarketContextBar/&gt;</c>.
/// </summary>
public sealed partial class MarketContextBar : UserControl
{
    private static readonly SolidColorBrush Green   = new(Color.FromArgb(255, 0x27, 0x96, 0x42));
    private static readonly SolidColorBrush Red     = new(Color.FromArgb(255, 0xC0, 0x39, 0x2B));
    private static readonly SolidColorBrush Amber   = new(Color.FromArgb(255, 0xE6, 0x7E, 0x22));
    private static readonly SolidColorBrush Neutral = new(Color.FromArgb(255, 0x80, 0x80, 0x80));

    public MarketContextBar()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var service = App.Container?.GetService<IMarketContextService>();
            if (service is null) { Ring.IsActive = false; Ring.Visibility = Visibility.Collapsed; return; }

            var ctx = await service.GetAsync();

            RegimeTextBlock.Text = ctx.RegimeText;
            RegimeBadge.Background = ctx.Regime switch
            {
                MarketRegime.RiskOn  => Green,
                MarketRegime.RiskOff => Red,
                _                    => Neutral,
            };
            ToolTipService.SetToolTip(RegimeBadge, ctx.RegimeSummary);

            FgTextBlock.Text = ctx.FearGreedText;
            FgTextBlock.Foreground = !ctx.HasFearGreed ? Neutral : ctx.FearGreedValue switch
            {
                <= 25 => Red,
                <= 45 => Amber,
                <= 55 => Neutral,
                _     => Green,
            };

            EventTextBlock.Text = ctx.NextEventText;
        }
        catch
        {
            // Context is een 'nice to have' — faal stil.
        }
        finally
        {
            Ring.IsActive = false;
            Ring.Visibility = Visibility.Collapsed;
        }
    }
}
