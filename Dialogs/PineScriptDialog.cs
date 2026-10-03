using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;

namespace CryptoPortfolioTracker.Dialogs;

/// <summary>
/// Toont een gegenereerd Pine Script (v1.48) met stappenuitleg: kopiëren, opslaan als .pine en de grafiek op
/// TradingView openen. Het venster blijft open na kopiëren/opslaan en toont een bevestiging.
/// </summary>
public sealed class PineScriptDialog : ContentDialog
{
    private readonly ITradingViewService _tv;
    private readonly string _script;
    private readonly string _fileLabel;
    private readonly TextBlock _status;

    public PineScriptDialog(string title, string script, string? ticker, string fileLabel, ElementTheme theme)
    {
        _tv        = App.Container.GetRequiredService<ITradingViewService>();
        _script    = script;
        _fileLabel = fileLabel;

        RequestedTheme           = theme;
        Title                    = $"📈 Pine Script — {title}";
        PrimaryButtonText        = "Kopiëren";
        SecondaryButtonText      = "Opslaan als .pine";
        CloseButtonText          = "Sluiten";
        DefaultButton            = ContentDialogButton.Primary;
        Resources["ContentDialogMaxWidth"] = 900.0;

        _status = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGoldenrod), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };

        var stack = new StackPanel { Spacing = 10, Width = 820 };
        stack.Children.Add(new TextBlock { Text = PineScriptGenerator.HowToUse, TextWrapping = TextWrapping.Wrap, FontSize = 13 });

        if (!string.IsNullOrWhiteSpace(ticker))
        {
            var open = new Button { Content = $"Open grafiek op TradingView ({ticker})" };
            open.Click += async (_, _) =>
            {
                bool ok = await _tv.OpenChartAsync(ticker);
                _status.Text = ok ? $"✓ TradingView geopend in je browser ({ticker})." : "Openen van de browser is mislukt.";
            };
            stack.Children.Add(open);
        }

        // Let op de volgorde: AcceptsReturn moet vóór Text staan. Een TextBox zonder AcceptsReturn knipt de
        // tekst af na de eerste regel (dan zie je alleen "//@version=6").
        var scriptBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Height = 360,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(scriptBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(scriptBox, ScrollBarVisibility.Auto);
        scriptBox.Text = script;
        stack.Children.Add(scriptBox);
        stack.Children.Add(_status);
        Content = stack;

        PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;   // open houden
            _status.Text = _tv.CopyToClipboard(_script)
                ? "✓ Gekopieerd — plak het in de Pine Editor van TradingView (Ctrl+V)."
                : "Kopiëren naar het klembord is mislukt.";
        };
        SecondaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                var path = await _tv.SaveAsync(PineScriptGenerator.FileName(_fileLabel, DateTime.Now), _script);
                _status.Text = $"✓ Opgeslagen: {path}";
                _tv.RevealInExplorer(path);
            }
            catch (Exception ex)
            {
                _status.Text = $"Opslaan mislukt: {ex.Message}";
            }
            finally { deferral.Complete(); }
        };
    }

    /// <summary>Toont het venster; vangt de fout af als er al een ander dialoogvenster open staat.</summary>
    public static async Task ShowAsync(XamlRoot root, string title, string script, string? ticker, string fileLabel, ElementTheme theme)
    {
        try
        {
            await new PineScriptDialog(title, script, ticker, fileLabel, theme) { XamlRoot = root }.ShowAsync();
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Pine Script-venster kon niet worden geopend");
        }
    }

    /// <summary>
    /// Slaat een TradingView-watchlist op en legt uit hoe je hem importeert.
    /// </summary>
    public static async Task ExportWatchlistAsync(XamlRoot root, string label, string content, int count, ElementTheme theme)
    {
        var tv = App.Container.GetRequiredService<ITradingViewService>();
        string message;
        try
        {
            var path = await tv.SaveAsync($"CPT_watchlist_{label}_{DateTime.Now:yyyyMMdd-HHmm}.txt", content);
            tv.RevealInExplorer(path);
            message = $"{count} symbolen opgeslagen in:\n{path}\n\n" +
                      "Importeren in TradingView: open de Watchlist (rechts) → ⋯ (meer) → 'Lijst importeren' → kies dit bestand.\n\n" +
                      "De lijst staat ook op je klembord.";
            tv.CopyToClipboard(content);
        }
        catch (Exception ex)
        {
            message = $"Opslaan mislukt: {ex.Message}";
        }

        try
        {
            await new ContentDialog
            {
                Title = "📋 TradingView-watchlist",
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
                CloseButtonText = "OK",
                RequestedTheme = theme,
                XamlRoot = root,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Watchlist-venster kon niet worden geopend");
        }
    }
}
