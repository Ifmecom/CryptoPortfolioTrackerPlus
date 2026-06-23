using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace CryptoPortfolioTracker.Views;

public sealed partial class TradeAnalysisView : Page
{
    private readonly TradeAnalysisViewModel _vm;

    public TradeAnalysisView(TradeAnalysisViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        InitializeComponent();
        DataContext = _vm;
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        // Always unsubscribe first to prevent duplicate subscriptions on re-navigation
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;

        await _vm.InitializeAsync();

        // Restore overview panel — persist until the user explicitly refreshes
        if (_vm.AllResults is not null)
        {
            OverviewPanel.Children.Clear();
            RenderAllResults(_vm.AllResults);
        }
        else
        {
            ShowOverviewPlaceholder();
        }

        // Restore single-coin analysis panel
        if (_vm.CurrentAnalysis is not null)
        {
            AnalysisPanel.Children.Clear();
            RenderAnalysis(_vm.CurrentAnalysis);
            CopyButton.IsEnabled = true;
        }
        else
        {
            ShowAnalysisPlaceholder();
            CopyButton.IsEnabled = false;
        }
    }

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OverviewDir_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (OverviewDirBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _vm.OverviewDir = tag;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Single-coin analysis complete → fill Analyse tab and switch to it
            case nameof(_vm.CurrentAnalysis):
                DispatcherQueue.TryEnqueue(() =>
                {
                    AnalysisPanel.Children.Clear();
                    if (_vm.CurrentAnalysis is not null)
                    {
                        RenderAnalysis(_vm.CurrentAnalysis);
                        CopyButton.IsEnabled = true;
                        ContentPivot.SelectedIndex = 1;   // switch to Analyse tab
                    }
                    else
                    {
                        ShowAnalysisPlaceholder();
                        CopyButton.IsEnabled = false;
                    }
                });
                break;

            // Bulk analysis complete → fill Overzicht tab and switch to it
            case nameof(_vm.AllResults):
                DispatcherQueue.TryEnqueue(() =>
                {
                    OverviewPanel.Children.Clear();
                    CopyButton.IsEnabled = false;
                    if (_vm.AllResults is not null)
                    {
                        RenderAllResults(_vm.AllResults);
                        ContentPivot.SelectedIndex = 0;   // switch to Overzicht tab
                    }
                    else
                    {
                        ShowOverviewPlaceholder();
                    }
                });
                break;

            // Direction filter changed → re-render the overview (stay on the current tab)
            case nameof(_vm.OverviewDir):
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_vm.AllResults is null) return;
                    OverviewPanel.Children.Clear();
                    RenderAllResults(_vm.AllResults);
                });
                break;

            // Bulk analysis started → show spinner in Overzicht tab and switch to it
            case nameof(_vm.IsAnalyzingAll):
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_vm.IsAnalyzingAll)
                    {
                        OverviewPanel.Children.Clear();
                        CopyButton.IsEnabled = false;
                        ShowAnalyzingAllPlaceholder();
                        ContentPivot.SelectedIndex = 0;   // switch to Overzicht tab
                    }
                });
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Placeholders
    // -----------------------------------------------------------------------

    private void ShowOverviewPlaceholder()
    {
        OverviewPanel.Children.Clear();
        OverviewPanel.Children.Add(new TextBlock
        {
            Text = "Klik 'Analyseer alles' voor een gerangschikte lijst van alle trade-signalen in je portfolio.",
            FontSize = 13,
            Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0),
        });
    }

    private void ShowAnalysisPlaceholder()
    {
        AnalysisPanel.Children.Clear();
        AnalysisPanel.Children.Add(new TextBlock
        {
            Text = "Selecteer een coin en klik 'Analyseer', of klik op een coin in het Overzicht-tabblad.",
            FontSize = 13,
            Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0),
        });
    }

    private void ShowAnalyzingAllPlaceholder()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 20, 0, 0) };
        sp.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
        sp.Children.Add(new TextBlock
        {
            Text = "Alle coins analyseren — dit duurt even...",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
        });
        OverviewPanel.Children.Add(sp);
    }

    // -----------------------------------------------------------------------
    // Ranked "Analyseer alles" results
    // -----------------------------------------------------------------------

    private void RenderAllResults(IReadOnlyList<CoinAnalysisSummary> allResults)
    {
        // Richtingsfilter toepassen (VM.OverviewDir)
        IEnumerable<CoinAnalysisSummary> view = _vm.OverviewDir switch
        {
            "Long"  => allResults.Where(r => r.Direction == "Long"),
            "Short" => allResults.Where(r => r.Direction == "Short"),
            "None"  => allResults.Where(r => r.Direction != "Long" && r.Direction != "Short"),
            _       => allResults,
        };
        var results = view.ToList();

        // Section header row: title + timestamp
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.Margin = new Thickness(0, 4, 0, 12);

        int signalCount = results.Count(r => r.Direction is "Long" or "Short");
        string filterNote = _vm.OverviewDir switch
        {
            "Long"  => "  ·  filter: Long",
            "Short" => "  ·  filter: Short",
            "None"  => "  ·  filter: geen signaal",
            _       => string.Empty,
        };
        var titleBlock = new TextBlock
        {
            Text       = $"Trade-signalen — {signalCount} signalen in {results.Count} coins{filterNote}",
            FontSize   = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleBlock, 0);

        var tsText = _vm.AllResultsGeneratedAt.HasValue
            ? $"Laatste analyse: {_vm.AllResultsGeneratedAt.Value:HH:mm:ss}"
            : string.Empty;
        var tsBlock = new TextBlock
        {
            Text      = tsText,
            FontSize  = 11,
            Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(tsBlock, 1);

        headerRow.Children.Add(titleBlock);
        headerRow.Children.Add(tsBlock);
        OverviewPanel.Children.Add(headerRow);
        OverviewPanel.Children.Add(TrendMomentumNote());

        string? lastDir = null;
        foreach (var s in results)
        {
            // Direction group header
            if (s.Direction != lastDir)
            {
                lastDir = s.Direction;
                var groupLabel = s.Direction == "Long"  ? "▲  Long signalen"  :
                                 s.Direction == "Short" ? "▼  Short signalen" :
                                                          "—  Geen signaal";
                var groupColor = s.Direction == "Long"  ? Green() :
                                 s.Direction == "Short" ? Red()   : Gray();

                OverviewPanel.Children.Add(new Border { Height = 10 });
                OverviewPanel.Children.Add(new TextBlock
                {
                    Text       = groupLabel,
                    FontSize   = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = groupColor,
                    Margin     = new Thickness(0, 0, 0, 4),
                });
            }

            OverviewPanel.Children.Add(BuildSummaryCard(s));
            OverviewPanel.Children.Add(new Border { Height = 4 });
        }
    }

    private Border BuildSummaryCard(CoinAnalysisSummary s)
    {
        var isLong  = s.Direction == "Long";
        var isShort = s.Direction == "Short";

        var card = new Border
        {
            Background      = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush     = isLong ? Green() : isShort ? Red() : Gray(),
        };
        card.PointerEntered += (_, _) => card.Opacity = 0.80;
        card.PointerExited  += (_, _) => card.Opacity = 1.00;

        var grid = new Grid { MinHeight = 42 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });           // logo
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });           // naam
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });           // richting+score
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // entry/SL/TP
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });           // bron+confidence

        // Col 0 — logo
        var img = new Image { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(s.Coin.ImageUri))
            try { img.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(s.Coin.ImageUri)); } catch { }
        Grid.SetColumn(img, 0);

        // Col 1 — naam + symbool
        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock { Text = s.Coin.Name, FontSize = 13, FontWeight = FontWeights.SemiBold });
        nameStack.Children.Add(new TextBlock
        {
            Text = $"{s.Coin.Symbol?.ToUpperInvariant()}  •  {s.Coin.Change24Hr:+0.0;-0.0}% 24h",
            FontSize = 11,
            Foreground = s.Coin.Change24Hr >= 0 ? Green() : Red(),
        });
        Grid.SetColumn(nameStack, 1);

        // Col 2 — richting badge + score
        var dirStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        if (isLong || isShort)
        {
            var badge = new Border
            {
                Background    = isLong ? Green() : Red(),
                CornerRadius  = new CornerRadius(3),
                Padding       = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            badge.Child = new TextBlock
            {
                Text       = isLong ? "▲ LONG" : "▼ SHORT",
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            };
            dirStack.Children.Add(badge);
        }
        dirStack.Children.Add(new TextBlock
        {
            Text       = $"Score {s.Score}/100",
            FontSize   = 11,
            Foreground = ScoreColor(s.Score),
        });
        Grid.SetColumn(dirStack, 2);

        // Col 3 — entry / SL / TP / R:R
        var tradeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        if (s.EntryPrice > 0)
        {
            tradeStack.Children.Add(new TextBlock
            {
                Text = $"Entry {FormatPrice(s.EntryPrice)}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            });
            var subRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            if (s.StopLossPct > 0)
                subRow.Children.Add(new TextBlock { Text = $"SL -{s.StopLossPct:F1}%", FontSize = 11, Foreground = Red() });
            if (s.Target1Pct > 0)
                subRow.Children.Add(new TextBlock { Text = $"TP +{s.Target1Pct:F1}%", FontSize = 11, Foreground = Green() });
            if (s.RiskReward1 > 0)
                subRow.Children.Add(new TextBlock { Text = $"R:R 1:{s.RiskReward1:F1}", FontSize = 11, Foreground = DarkGold() });
            tradeStack.Children.Add(subRow);
        }
        else
        {
            tradeStack.Children.Add(new TextBlock
            {
                Text = "Geen setup",
                FontSize = 11,
                Foreground = Gray(),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        Grid.SetColumn(tradeStack, 3);

        // Col 4 — databron + confidence
        var metaStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 3 };
        if (!string.IsNullOrEmpty(s.Confidence) && s.Confidence != "–")
            metaStack.Children.Add(new TextBlock
            {
                Text = s.Confidence,
                FontSize = 11,
                Foreground = ConfidenceColor(s.Confidence),
                HorizontalAlignment = HorizontalAlignment.Right,
            });

        // Compact data source badge
        var srcText = s.DataSource.Contains("Binance") ? "Binance" :
                      s.DataSource.Contains("KuCoin")  ? "KuCoin"  :
                      s.DataSource.Contains("Gate")    ? "Gate.io" :
                      s.DataSource.Contains("MEXC")    ? "MEXC"    : "Cache";
        metaStack.Children.Add(new TextBlock
        {
            Text = srcText,
            FontSize = 10,
            Foreground = s.HasLiveData ? Blue() : Orange(),
            HorizontalAlignment = HorizontalAlignment.Right,
        });
        Grid.SetColumn(metaStack, 4);

        grid.Children.Add(img);
        grid.Children.Add(nameStack);
        grid.Children.Add(dirStack);
        grid.Children.Add(tradeStack);
        grid.Children.Add(metaStack);

        card.Child = grid;

        // Klik → volledige analyse
        card.Tapped += async (_, _) => await _vm.AnalyzeCoinFromSummaryAsync(s);

        // Hover effect
        card.PointerEntered += (_, _) =>
            card.Background = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundChromeMediumBrush"];
        card.PointerExited += (_, _) =>
            card.Background = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"];

        return card;
    }

    private static SolidColorBrush ScoreColor(int score)
        => score >= 60 ? Green() : score <= 40 ? Red() : Orange();

    // -----------------------------------------------------------------------
    // Main renderer
    // -----------------------------------------------------------------------

    private void RenderAnalysis(TradeAnalysisResult r)
    {
        // ── Summary header card ──────────────────────────────────────────
        AnalysisPanel.Children.Add(BuildSummaryCard(r));
        AnalysisPanel.Children.Add(Spacer(12));

        // ── Timeframe sections ───────────────────────────────────────────
        if (r.Weekly.HasData)      { AnalysisPanel.Children.Add(BuildTimeframeSection(r.Weekly,      r.CurrentPrice)); AnalysisPanel.Children.Add(Spacer(8)); }
        if (r.Daily.HasData)       { AnalysisPanel.Children.Add(BuildTimeframeSection(r.Daily,       r.CurrentPrice)); AnalysisPanel.Children.Add(Spacer(8)); }
        if (r.FourHour.HasData)    { AnalysisPanel.Children.Add(BuildTimeframeSection(r.FourHour,    r.CurrentPrice)); AnalysisPanel.Children.Add(Spacer(8)); }
        if (r.OneHour.HasData)     { AnalysisPanel.Children.Add(BuildTimeframeSection(r.OneHour,     r.CurrentPrice)); AnalysisPanel.Children.Add(Spacer(8)); }
        if (r.FifteenMin.HasData)  { AnalysisPanel.Children.Add(BuildTimeframeSection(r.FifteenMin,  r.CurrentPrice)); AnalysisPanel.Children.Add(Spacer(8)); }

        // ── Key levels ───────────────────────────────────────────────────
        if (r.ResistanceLevels.Any() || r.SupportLevels.Any())
        {
            AnalysisPanel.Children.Add(BuildKeyLevelsSection(r));
            AnalysisPanel.Children.Add(Spacer(8));
        }

        // ── Trade setup ──────────────────────────────────────────────────
        AnalysisPanel.Children.Add(BuildTradeSetupSection(r));
        AnalysisPanel.Children.Add(Spacer(8));

        // ── Footnote — bron-melding ──────────────────────────────────────
        if (!r.BinanceDataAvailable)
        {
            bool isKuCoin    = r.DataSource.StartsWith("KuCoin",     StringComparison.OrdinalIgnoreCase);
            bool isLocalOnly = r.DataSource.Contains("lokale cache", StringComparison.OrdinalIgnoreCase);

            string text = isKuCoin
                ? $"ℹ️  Coin niet op Binance — data opgehaald via {r.DataSource}."
                : isLocalOnly
                    ? "⚠️  Coin niet op Binance of KuCoin — analyse gebaseerd op de lokale koerscache (alleen dagelijkse slotkoersen, geen 4H/1H data)."
                    : $"⚠️  Geen live data beschikbaar — {r.DataSource}.";

            AnalysisPanel.Children.Add(new TextBlock
            {
                Text         = text,
                FontSize     = 11,
                Foreground   = isKuCoin ? Blue() : Orange(),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 4, 0, 0),
            });
        }
    }

    // -----------------------------------------------------------------------
    // Summary card
    // -----------------------------------------------------------------------

    private Border BuildSummaryCard(TradeAnalysisResult r)
    {
        var card = Card(new Thickness(0, 8, 0, 0));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Coin logo
        var img = new Image { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(r.ImageUri))
        {
            try { img.Source = new BitmapImage(new Uri(r.ImageUri)); } catch { }
        }
        Grid.SetColumn(img, 0);

        // Name + price block
        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock
        {
            Text = $"{r.CoinName}  ({r.Symbol})",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
        });
        var priceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        priceRow.Children.Add(new TextBlock
        {
            Text = FormatPrice(r.CurrentPrice),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
        });
        double ch = r.Change24h;
        priceRow.Children.Add(new TextBlock
        {
            Text = $"{ch:+0.00;-0.00}% 24h",
            FontSize = 13,
            Foreground = ch >= 0 ? Green() : Red(),
        });
        nameStack.Children.Add(priceRow);
        Grid.SetColumn(nameStack, 1);

        // Score + direction + regime block
        var rightStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4,
        };

        if (r.CombinedScore > 0)
        {
            var scoreBadge = new Border
            {
                Background = ScoreBackground(r.CombinedScore),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
            };
            scoreBadge.Child = new TextBlock
            {
                Text = $"Score {r.CombinedScore}/100  •  {r.Direction}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
            };
            rightStack.Children.Add(scoreBadge);
        }

        if (!string.IsNullOrEmpty(r.Regime) && r.Regime != "Unknown")
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = $"Regime: {r.Regime}",
                FontSize = 12,
                Foreground = RegimeColor(r.Regime),
                HorizontalAlignment = HorizontalAlignment.Right,
            });
        }

        rightStack.Children.Add(new TextBlock
        {
            Text = $"Gegenereerd: {r.GeneratedAt:HH:mm:ss}",
            FontSize = 11,
            Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            HorizontalAlignment = HorizontalAlignment.Right,
        });
        Grid.SetColumn(rightStack, 2);

        grid.Children.Add(img);
        grid.Children.Add(nameStack);
        grid.Children.Add(rightStack);

        card.Child = grid;
        return card;
    }

    // -----------------------------------------------------------------------
    // Timeframe section
    // -----------------------------------------------------------------------

    private Border BuildTimeframeSection(TimeframeAnalysis tf, double price)
    {
        var outer = Card();

        var col = new StackPanel { Spacing = 8 };

        // Section header
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        headerRow.Children.Add(new TextBlock
        {
            Text = $"📅  {tf.Label}",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
        });

        // Trend badge
        var trendBadge = new Border
        {
            Background = TrendBackground(tf.TrendBias),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        trendBadge.Child = new TextBlock
        {
            Text = tf.TrendBias,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
        };
        headerRow.Children.Add(trendBadge);

        col.Children.Add(headerRow);

        // Quick indicator strip
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        if (tf.Rsi > 0)   strip.Children.Add(IndicatorChip("RSI",  $"{tf.Rsi:F0}",  RsiColor(tf.Rsi)));
        if (tf.Macd != 0) strip.Children.Add(IndicatorChip("MACD", $"{tf.Macd:+0.00;-0.00}", tf.Macd > tf.MacdSignal ? Green() : Red()));
        if (tf.Adx > 0)   strip.Children.Add(IndicatorChip("ADX",  $"{tf.Adx:F0}",  AdxColor(tf.Adx)));
        if (tf.PctB > 0)  strip.Children.Add(IndicatorChip("%B",   $"{tf.PctB:F0}", PctBColor(tf.PctB)));
        if (tf.IsSqueeze) strip.Children.Add(IndicatorChip("Squeeze", "Aan", Orange()));
        col.Children.Add(strip);

        // Separator
        col.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) });

        // Bullet points
        foreach (var bullet in tf.Bullets)
        {
            col.Children.Add(new TextBlock
            {
                Text = "  " + bullet,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            });
        }

        outer.Child = col;
        return outer;
    }

    // -----------------------------------------------------------------------
    // Key levels section
    // -----------------------------------------------------------------------

    private Border BuildKeyLevelsSection(TradeAnalysisResult r)
    {
        var card = Card();
        var col = new StackPanel { Spacing = 10 };

        col.Children.Add(new TextBlock
        {
            Text = "🎯  Sleutelniveaus",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
        });

        col.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Resistance column
        var resCol = new StackPanel { Spacing = 4 };
        resCol.Children.Add(new TextBlock { Text = "Weerstand (resistance)", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Red() });
        foreach (var level in r.ResistanceLevels)
        {
            double pct = (level - r.CurrentPrice) / r.CurrentPrice * 100;
            resCol.Children.Add(new TextBlock
            {
                Text = $"  {FormatPrice(level)}  (+{pct:F1}%)",
                FontSize = 12,
                Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            });
        }
        if (!r.ResistanceLevels.Any())
            resCol.Children.Add(new TextBlock { Text = "  Geen duidelijk niveau gevonden", FontSize = 12, Foreground = Gray() });

        // Support column
        var supCol = new StackPanel { Spacing = 4 };
        supCol.Children.Add(new TextBlock { Text = "Steun (support)", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Green() });
        foreach (var level in r.SupportLevels)
        {
            double pct = (level - r.CurrentPrice) / r.CurrentPrice * 100;
            supCol.Children.Add(new TextBlock
            {
                Text = $"  {FormatPrice(level)}  ({pct:F1}%)",
                FontSize = 12,
                Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            });
        }
        if (!r.SupportLevels.Any())
            supCol.Children.Add(new TextBlock { Text = "  Geen duidelijk niveau gevonden", FontSize = 12, Foreground = Gray() });

        Grid.SetColumn(resCol, 0);
        Grid.SetColumn(supCol, 1);
        grid.Children.Add(resCol);
        grid.Children.Add(supCol);
        col.Children.Add(grid);

        card.Child = col;
        return card;
    }

    // -----------------------------------------------------------------------
    // Trade setup section
    // -----------------------------------------------------------------------

    private Border BuildTradeSetupSection(TradeAnalysisResult r)
    {
        var setup = r.Setup;

        // Accent border color based on direction
        Color accentColor = setup.Direction == "Long"  ? Color.FromArgb(0xFF, 0x20, 0xC2, 0x6A)
                          : setup.Direction == "Short" ? Color.FromArgb(0xFF, 0xE8, 0x40, 0x40)
                          :                              Color.FromArgb(0xFF, 0xB8, 0x86, 0x0B);

        var card = new Border
        {
            Background = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 12, 16, 14),
            Margin = new Thickness(0, 0, 0, 0),
            BorderThickness = new Thickness(0, 0, 0, 3),
            BorderBrush = new SolidColorBrush(accentColor),
        };

        var col = new StackPanel { Spacing = 10 };

        // Header
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        headerRow.Children.Add(new TextBlock
        {
            Text = "📋  Trade Setup",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
        });

        if (!string.IsNullOrEmpty(setup.Direction) && setup.Direction != "Geen signaal")
        {
            var dirBadge = DirectionBadge(setup.Direction);
            headerRow.Children.Add(dirBadge);

            // Paper Trade button — only shown when there is a real Long/Short signal
            var paperBtn = new Button
            {
                Content           = "📝  Paper Trade",
                Margin            = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Style             = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            paperBtn.Click += async (_, _) => await _vm.PlacePaperTradeCommand.ExecuteAsync(null);
            headerRow.Children.Add(paperBtn);
        }

        if (!string.IsNullOrEmpty(setup.Confidence) && setup.Confidence != "–")
        {
            headerRow.Children.Add(new TextBlock
            {
                Text = $"Betrouwbaarheid: {setup.Confidence}",
                FontSize = 12,
                Foreground = ConfidenceColor(setup.Confidence),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        col.Children.Add(headerRow);
        col.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) });
        col.Children.Add(TrendMomentumNote());

        // Geen signaal
        if (setup.Direction == "Geen signaal")
        {
            foreach (var line in setup.Reasoning)
                col.Children.Add(BulletText(line));
            card.Child = col;
            return card;
        }

        // Price grid: Entry / SL / TP1 / TP2
        var priceGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddPriceCell(priceGrid, 0, "Instap",      FormatPrice(setup.EntryPrice), "", Neutral());
        AddPriceCell(priceGrid, 1, "Stop-loss",   FormatPrice(setup.StopLoss),  $"-{setup.StopLossPct:F1}%", Red());
        AddPriceCell(priceGrid, 2, "Target 1",    FormatPrice(setup.Target1),   $"+{setup.Target1Pct:F1}%  R/R 1:{setup.RiskReward1:F1}", Green());
        AddPriceCell(priceGrid, 3, "Target 2",    FormatPrice(setup.Target2),   $"+{setup.Target2Pct:F1}%  R/R 1:{setup.RiskReward2:F1}", DarkGold());
        col.Children.Add(priceGrid);

        // Validatie-waarschuwing (ongeldige niveaus of krappe R/R)
        if (!string.IsNullOrEmpty(setup.ValidationWarning))
        {
            col.Children.Add(new InfoBar
            {
                IsOpen     = true,
                IsClosable = false,
                Severity   = setup.IsValid ? InfoBarSeverity.Warning : InfoBarSeverity.Error,
                Title      = setup.IsValid ? "Let op" : "Ongeldige setup",
                Message    = setup.ValidationWarning,
                Margin     = new Thickness(0, 4, 0, 0),
            });
        }

        // Entry note
        if (!string.IsNullOrEmpty(setup.EntryNote))
        {
            col.Children.Add(new TextBlock
            {
                Text = "💡  " + setup.EntryNote,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
            });
        }

        col.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)) });

        // Reasoning bullets
        col.Children.Add(new TextBlock
        {
            Text = "Onderbouwing:",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
        });
        foreach (var line in setup.Reasoning)
            col.Children.Add(BulletText(line));

        // ── Verrijking: liquiditeit / positionering / event-risico ──────────
        if (r.OrderBook is not null || r.Positioning is { IsAvailable: true } || r.HasEventRisk)
        {
            col.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)) });
            col.Children.Add(new TextBlock { Text = "Markt-context:", FontSize = 12, FontWeight = FontWeights.SemiBold });

            if (r.OrderBook is not null)
                col.Children.Add(BulletText($"💧 Liquiditeit: {r.LiquidityDisplay}"));

            if (r.Positioning is { IsAvailable: true })
                col.Children.Add(BulletText($"📉 Positionering: funding {r.FundingDisplay}"));

            foreach (var ev in r.MacroEvents)
            {
                var evBullet = BulletText($"⚠ Event-risico: {ev.ShortDisplay}");
                evBullet.Foreground = Orange();
                col.Children.Add(evBullet);
            }
        }

        card.Child = col;
        return card;
    }

    // -----------------------------------------------------------------------
    // UI helpers
    // -----------------------------------------------------------------------

    private static Border Card(Thickness? margin = null) => new()
    {
        Background    = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
        CornerRadius  = new CornerRadius(6),
        Padding       = new Thickness(16, 12, 16, 14),
        Margin        = margin ?? new Thickness(0, 0, 0, 0),
    };

    private static UIElement Spacer(double height) => new Border { Height = height };

    private static FrameworkElement IndicatorChip(string label, string value, SolidColorBrush valueBrush)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)) });
        sp.Children.Add(new TextBlock { Text = value,  FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = valueBrush });
        return sp;
    }

    private static void AddPriceCell(Grid grid, int col, string label, string price, string subtext, SolidColorBrush priceColor)
    {
        var sp = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 12, 0) };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)) });
        sp.Children.Add(new TextBlock { Text = price, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = priceColor });
        if (!string.IsNullOrEmpty(subtext))
            sp.Children.Add(new TextBlock { Text = subtext, FontSize = 11, Foreground = priceColor });
        Grid.SetColumn(sp, col);
        grid.Children.Add(sp);
    }

    private static Border DirectionBadge(string direction)
    {
        var badge = new Border
        {
            Background    = direction == "Long" ? Green() : Red(),
            CornerRadius  = new CornerRadius(3),
            Padding       = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock
        {
            Text = direction == "Long" ? "▲  LONG" : "▼  SHORT",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
        };
        return badge;
    }

    private static TextBlock BulletText(string text) => new()
    {
        Text = "  • " + text,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
    };

    /// <summary>
    /// Vaste toelichting dat Trade Advies een trend/momentum-advies is zónder chartpatronen —
    /// zodat het verschil met Pattern Trading (dat patronen wél meeweegt) duidelijk is.
    /// </summary>
    private static TextBlock TrendMomentumNote() => new()
    {
        Text = "ℹ️  Trend- & momentum-advies — chartpatronen worden hier níét meegewogen. "
             + "Voor patroongedreven setups (die tegengesteld kunnen uitvallen): zie Pattern Trading.",
        FontSize = 11,
        FontStyle = Windows.UI.Text.FontStyle.Italic,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
        Margin = new Thickness(0, 0, 0, 8),
    };

    // ── Colors ───────────────────────────────────────────────────────────────

    private static SolidColorBrush Green()    => new(Color.FromArgb(0xFF, 0x20, 0xC2, 0x6A));
    private static SolidColorBrush Red()      => new(Color.FromArgb(0xFF, 0xE8, 0x40, 0x40));
    private static SolidColorBrush Orange()   => new(Color.FromArgb(0xFF, 0xFF, 0x8C, 0x00));
    private static SolidColorBrush Blue()     => new(Color.FromArgb(0xFF, 0x00, 0xA8, 0xE8));
    private static SolidColorBrush DarkGold() => new(Color.FromArgb(0xFF, 0xB8, 0x86, 0x0B));
    private static SolidColorBrush Gray()     => new(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
    private static SolidColorBrush Neutral()  => new(Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC));

    private static SolidColorBrush RsiColor(double rsi)
        => rsi <= 30 ? Green() : rsi >= 70 ? Red() : Neutral();

    private static SolidColorBrush AdxColor(double adx)
        => adx >= 25 ? Orange() : Gray();

    private static SolidColorBrush PctBColor(double pctB)
        => pctB <= 15 ? Green() : pctB >= 85 ? Red() : Neutral();

    private static SolidColorBrush RegimeColor(string regime)
        => regime == "RiskOn" ? Green() : regime == "RiskOff" ? Red() : Orange();

    private static SolidColorBrush ConfidenceColor(string c)
        => c == "Hoog" ? Green() : c == "Gemiddeld" ? Orange() : Gray();

    private static SolidColorBrush ScoreBackground(int score)
        => score >= 60 ? new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x8C, 0x50))
         : score <= 40 ? new SolidColorBrush(Color.FromArgb(0xFF, 0xB0, 0x2A, 0x2A))
         :               new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x6A, 0x00));

    private static SolidColorBrush TrendBackground(string bias)
        => bias == "Bullish" ? new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x8C, 0x50))
         : bias == "Bearish" ? new SolidColorBrush(Color.FromArgb(0xFF, 0xB0, 0x2A, 0x2A))
         :                     new SolidColorBrush(Color.FromArgb(0xFF, 0x55, 0x55, 0x55));

    // ── Price formatter ───────────────────────────────────────────────────────

    private static string FormatPrice(double price)
        => price >= 10000 ? $"${price:N0}"
         : price >= 1000  ? $"${price:N1}"
         : price >= 100   ? $"${price:N2}"
         : price >= 10    ? $"${price:N3}"
         : price >= 1     ? $"${price:N4}"
         : price >= 0.01  ? $"${price:N5}"
         :                   $"${price:N6}";

    // -----------------------------------------------------------------------
    // Copy to clipboard
    // -----------------------------------------------------------------------

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentAnalysis is null) return;

        var text = FormatForDiscord(_vm.CurrentAnalysis);

        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);

        // Brief visual feedback
        CopyButton.Content = "✅  Gekopieerd!";
        CopyButton.IsEnabled = false;
        await Task.Delay(2000);
        CopyButton.Content = "📋  Kopieer";
        CopyButton.IsEnabled = true;
    }

    private static string FormatForDiscord(TradeAnalysisResult r)
    {
        var sb = new StringBuilder();
        var fp = (double p) => FormatPrice(p);

        // ── Header ────────────────────────────────────────────────────────
        sb.AppendLine($"## 📊 Trade Analyse — {r.CoinName} ({r.Symbol})");
        sb.AppendLine($"Prijs: **{fp(r.CurrentPrice)}**  |  24h: {r.Change24h:+0.00;-0.00}%  |  Score: **{r.CombinedScore}/100 {r.Direction}**");
        if (!string.IsNullOrWhiteSpace(r.Regime) && r.Regime != "Unknown")
            sb.AppendLine($"Markt regime: {r.Regime}");
        sb.AppendLine($"*Gegenereerd: {r.GeneratedAt:dd-MM-yyyy HH:mm}*");
        sb.AppendLine();

        // ── Timeframes ────────────────────────────────────────────────────
        foreach (var tf in new[] { r.Weekly, r.Daily, r.FourHour, r.OneHour, r.FifteenMin })
        {
            if (!tf.HasData) continue;

            sb.AppendLine($"### 📅 {tf.Label}  [{tf.TrendBias}]");

            // Quick indicator line
            var chips = new List<string>();
            if (tf.Rsi > 0)   chips.Add($"RSI {tf.Rsi:F0}");
            if (tf.Adx > 0)   chips.Add($"ADX {tf.Adx:F0}");
            if (tf.Macd != 0) chips.Add($"MACD {tf.Macd:+0.00;-0.00}");
            if (tf.PctB > 0)  chips.Add($"%B {tf.PctB:F0}");
            if (tf.IsSqueeze) chips.Add("Squeeze AAN");
            if (chips.Any())
                sb.AppendLine(string.Join("  •  ", chips));

            foreach (var bullet in tf.Bullets)
                sb.AppendLine($"- {bullet}");

            sb.AppendLine();
        }

        // ── Key levels ────────────────────────────────────────────────────
        if (r.ResistanceLevels.Any() || r.SupportLevels.Any())
        {
            sb.AppendLine("### 🎯 Sleutelniveaus");

            if (r.ResistanceLevels.Any())
            {
                sb.Append("**Weerstand:** ");
                sb.AppendLine(string.Join("  |  ", r.ResistanceLevels.Select(lvl =>
                {
                    double pct = (lvl - r.CurrentPrice) / r.CurrentPrice * 100;
                    return $"{fp(lvl)} (+{pct:F1}%)";
                })));
            }

            if (r.SupportLevels.Any())
            {
                sb.Append("**Steun:** ");
                sb.AppendLine(string.Join("  |  ", r.SupportLevels.Select(lvl =>
                {
                    double pct = (lvl - r.CurrentPrice) / r.CurrentPrice * 100;
                    return $"{fp(lvl)} ({pct:F1}%)";
                })));
            }

            sb.AppendLine();
        }

        // ── Trade setup ───────────────────────────────────────────────────
        var s = r.Setup;
        sb.AppendLine($"### 📋 Trade Setup — {s.Direction}");

        if (s.Direction != "Geen signaal" && s.EntryPrice > 0)
        {
            sb.AppendLine($"| | Prijs | % |");
            sb.AppendLine($"|---|---|---|");
            sb.AppendLine($"| **Instap**    | {fp(s.EntryPrice)} | — |");
            sb.AppendLine($"| **Stop-loss** | {fp(s.StopLoss)}   | -{s.StopLossPct:F1}% |");
            sb.AppendLine($"| **Target 1**  | {fp(s.Target1)}    | +{s.Target1Pct:F1}%  (R/R 1:{s.RiskReward1:F1}) |");
            sb.AppendLine($"| **Target 2**  | {fp(s.Target2)}    | +{s.Target2Pct:F1}%  (R/R 1:{s.RiskReward2:F1}) |");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(s.EntryNote))
                sb.AppendLine($"💡 {s.EntryNote}");

            if (!string.IsNullOrEmpty(s.Confidence) && s.Confidence != "–")
                sb.AppendLine($"Betrouwbaarheid: **{s.Confidence}**");

            sb.AppendLine();
        }

        if (s.Reasoning.Any())
        {
            sb.AppendLine("**Onderbouwing:**");
            foreach (var line in s.Reasoning)
                sb.AppendLine($"- {line}");
        }

        if (!r.BinanceDataAvailable)
        {
            sb.AppendLine();
            bool isKuCoin = r.DataSource.StartsWith("KuCoin", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine(isKuCoin
                ? $"ℹ️ *Coin niet op Binance — data via {r.DataSource}.*"
                : "⚠️ *Coin niet op Binance of KuCoin — analyse gebaseerd op lokale koerscache (alleen dagslotkoersen).*");
        }

        sb.AppendLine();
        sb.AppendLine("*Gegenereerd door Crypto Portfolio Tracker Plus*");

        return sb.ToString();
    }
}
