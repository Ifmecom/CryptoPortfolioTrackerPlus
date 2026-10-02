using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace CryptoPortfolioTracker.Controls;

/// <summary>
/// Gedeelde "Top X"-balk (v1.48) voor elke pagina met setups of adviezen:
/// <c>🏆 Top [Uit/3/5/10/20] · Markeren ⇄ Alleen top · ? · "top 5 van 23 kansen"</c>.
/// Bind <see cref="TopCount"/> en <see cref="OnlyTop"/> TwoWay aan de ViewModel; de rangschikking zelf
/// gebeurt in <see cref="OpportunityRanker"/>. Code-only (geen XAML) zodat hij overal zonder XAML-compilatie
/// van een eigen bestand bruikbaar is.
/// </summary>
public sealed class TopPicksBar : UserControl
{
    private static readonly SolidColorBrush Gold = new(Microsoft.UI.Colors.DarkGoldenrod);

    private readonly ComboBox     _count;
    private readonly ToggleSwitch _onlyTop;
    private readonly TextBlock    _summary;
    private bool _syncing;

    public static readonly DependencyProperty TopCountProperty = DependencyProperty.Register(
        nameof(TopCount), typeof(int), typeof(TopPicksBar), new PropertyMetadata(5, (d, _) => ((TopPicksBar)d).SyncFromProperties()));

    public static readonly DependencyProperty OnlyTopProperty = DependencyProperty.Register(
        nameof(OnlyTop), typeof(bool), typeof(TopPicksBar), new PropertyMetadata(false, (d, _) => ((TopPicksBar)d).SyncFromProperties()));

    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(string), typeof(TopPicksBar), new PropertyMetadata(string.Empty, (d, e) => ((TopPicksBar)d)._summary.Text = e.NewValue as string ?? string.Empty));

    /// <summary>Aantal te markeren setups; 0 = uit.</summary>
    public int TopCount { get => (int)GetValue(TopCountProperty); set => SetValue(TopCountProperty, value); }

    /// <summary>Alleen de top tonen (true) of de top in de volledige lijst markeren (false).</summary>
    public bool OnlyTop { get => (bool)GetValue(OnlyTopProperty); set => SetValue(OnlyTopProperty, value); }

    /// <summary>Korte toelichting rechts, bijv. "top 5 van 23 kansen".</summary>
    public string Summary { get => (string)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    public TopPicksBar()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        panel.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "", FontSize = 15,
            Foreground = Gold, VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(new TextBlock { Text = "Top", VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        _count = new ComboBox { MinWidth = 76, VerticalAlignment = VerticalAlignment.Center };
        foreach (var n in OpportunityRanker.TopOptions)
            _count.Items.Add(n == 0 ? "Uit" : n.ToString());
        ToolTipService.SetToolTip(_count, "Hoeveel van de meest kansrijke setups markeren (Uit = geen)");
        _count.SelectionChanged += (_, _) =>
        {
            if (_syncing || _count.SelectedIndex < 0) return;
            TopCount = OpportunityRanker.TopOptions[_count.SelectedIndex];
        };
        panel.Children.Add(_count);

        _onlyTop = new ToggleSwitch
        {
            OffContent = "Markeren", OnContent = "Alleen top", MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0),
        };
        ToolTipService.SetToolTip(_onlyTop, "Markeren: de top krijgt een gouden rand in de volledige lijst.\nAlleen top: toon alleen de top X, op volgorde.");
        _onlyTop.Toggled += (_, _) => { if (!_syncing) OnlyTop = _onlyTop.IsOn; };
        panel.Children.Add(_onlyTop);

        var help = new Button
        {
            Content = new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "", FontSize = 13 },
            Padding = new Thickness(6, 4, 6, 4), VerticalAlignment = VerticalAlignment.Center,
            Flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.Bottom,
                Content = new TextBlock { Text = OpportunityRanker.HowItWorks, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 },
            },
        };
        ToolTipService.SetToolTip(help, "Hoe wordt de top bepaald?");
        AutomationProperties.SetName(help, "Uitleg Top X");
        panel.Children.Add(help);

        _summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.75, FontSize = 12, Margin = new Thickness(4, 0, 0, 0) };
        panel.Children.Add(_summary);

        Content = panel;
        SyncFromProperties();
    }

    private void SyncFromProperties()
    {
        if (_count is null) return;
        _syncing = true;
        try
        {
            var idx = -1;
            for (int i = 0; i < OpportunityRanker.TopOptions.Count; i++)
                if (OpportunityRanker.TopOptions[i] == TopCount) idx = i;
            _count.SelectedIndex = idx >= 0 ? idx : 2;   // onbekende waarde → 5
            _onlyTop.IsOn = OnlyTop;
            _onlyTop.IsEnabled = TopCount > 0;
        }
        finally { _syncing = false; }
    }
}
