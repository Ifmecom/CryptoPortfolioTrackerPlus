using System.Text.Json;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace CryptoPortfolioTracker.Views;

/// <summary>
/// AI Research (v1.49). Alleen UI: de websites (WebView2, één tab per aanbieder, lui aangemaakt),
/// klembord en vensters. Logica zit in <see cref="AiResearchViewModel"/>.
/// </summary>
public sealed partial class AiResearchView : Page
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(AiResearchView).PadRight(22));

    public readonly AiResearchViewModel _viewModel;

    // Eén gedeelde WebView2-omgeving met een eigen profielmap: logins (cookies) blijven bewaard tussen sessies.
    private static Task<CoreWebView2Environment>? _environment;
    private readonly Dictionary<PivotItem, (AiWebProvider Provider, WebView2 View, TextBlock Hint)> _sites = new();
    private bool _tabsBuilt;

    public AiResearchView(AiResearchViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        BuildSiteTabs();
        using (PerfLog.Measure("AiResearchView.ViewLoadingAsync")) await _viewModel.ViewLoadingAsync();
        UpdateButtonsForTab();
    }

    private void View_Unloaded(object sender, RoutedEventArgs e) => _viewModel.Terminate();

    // ── Website-tabbladen ────────────────────────────────────────────────────

    private void BuildSiteTabs()
    {
        if (_tabsBuilt) return;
        _tabsBuilt = true;

        foreach (var provider in AiCatalog.WebProviders)
        {
            var hint = new TextBlock
            {
                Text = provider.Note, FontSize = 11, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var openExternal = new Button { Content = "Open in browser", Padding = new Thickness(8, 3, 8, 3), FontSize = 12 };
            ToolTipService.SetToolTip(openExternal, "Open deze site in je eigen browser (bijv. als inloggen via Google hier geweigerd wordt)");
            var reload = new Button { Content = "↻", Padding = new Thickness(8, 3, 8, 3), FontSize = 12 };
            ToolTipService.SetToolTip(reload, "Startpagina opnieuw laden");

            var view = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

            var bar = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 6) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.Children.Add(hint);
            Grid.SetColumn(reload, 1); bar.Children.Add(reload);
            Grid.SetColumn(openExternal, 2); bar.Children.Add(openExternal);

            var root = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(bar);
            var frame = new Border
            {
                CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Child = view,
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };
            Grid.SetRow(frame, 1);
            root.Children.Add(frame);

            var item = new PivotItem { Header = provider.Name, Content = root };
            _sites[item] = (provider, view, hint);
            ProviderPivot.Items.Add(item);

            openExternal.Click += async (_, _) =>
                await Launcher.LaunchUriAsync(new Uri(view.Source?.AbsoluteUri is { Length: > 0 } u && u != "about:blank" ? u : provider.HomeUrl));
            reload.Click += async (_, _) => await NavigateSiteAsync(item, provider.HomeUrl);
        }
    }

    private static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        var folder = Path.Combine(AppConstants.AppDataPath, "WebView2-AI");
        return _environment ??= CoreWebView2Environment.CreateWithOptionsAsync(null, folder, null).AsTask();
    }

    private async Task<WebView2?> EnsureSiteAsync(PivotItem item)
    {
        if (!_sites.TryGetValue(item, out var site)) return null;
        if (site.View.CoreWebView2 is not null) return site.View;
        try
        {
            await site.View.EnsureCoreWebView2Async(await GetEnvironmentAsync());
            site.View.CoreWebView2.Settings.AreDevToolsEnabled = false;
            if (site.View.Source is null || site.View.Source.AbsoluteUri == "about:blank")
                site.View.CoreWebView2.Navigate(site.Provider.HomeUrl);
            return site.View;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "WebView2 voor {Site} kon niet starten", site.Provider.Name);
            site.Hint.Text = "De ingebouwde browser (WebView2) kon niet starten. Gebruik 'Open in browser'.";
            return null;
        }
    }

    private async Task NavigateSiteAsync(PivotItem item, string url)
    {
        var view = await EnsureSiteAsync(item);
        view?.CoreWebView2?.Navigate(url);
    }

    private PivotItem? CurrentSiteItem => ProviderPivot.SelectedItem is PivotItem p && _sites.ContainsKey(p) ? p : null;

    private async void ProviderPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonsForTab();
        if (CurrentSiteItem is { } item) await EnsureSiteAsync(item);
    }

    private void UpdateButtonsForTab()
    {
        bool site = CurrentSiteItem is not null;
        SendButton.Content = site ? $"Naar {_sites[CurrentSiteItem!].Provider.Name}" : "Vraag stellen";
        AnalyzeButton.Visibility = site ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Versturen ────────────────────────────────────────────────────────────

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void QuestionBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                       .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (e.Key == VirtualKey.Enter && ctrl)
        {
            e.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Question)) return;

        if (CurrentSiteItem is not { } item)
        {
            await _viewModel.AskCommand.ExecuteAsync(null);
            ChatList.UpdateLayout();
            if (_viewModel.Chat.Count > 0) ChatList.ScrollIntoView(_viewModel.Chat[^1]);
            return;
        }

        var provider = _sites[item].Provider;

        // Context gaat nooit in een URL: die komt (met de vraag) op het klembord om te plakken.
        if (_viewModel.IncludeContext || provider.QueryUrlTemplate is null)
        {
            CopyToClipboard(await _viewModel.BuildWebPromptAsync(_viewModel.IncludeContext));
            await NavigateSiteAsync(item, provider.HomeUrl);
            _viewModel.StatusText = $"Vraag{(_viewModel.IncludeContext ? " + portfolio-context" : string.Empty)} staat op het klembord: klik in het invoerveld van {provider.Name}, plak (Ctrl+V) en verstuur.";
        }
        else
        {
            await NavigateSiteAsync(item, AiCatalog.UrlFor(provider, _viewModel.Question));
            _viewModel.StatusText = $"Vraag geopend in {provider.Name}. Selecteer straks het antwoord en klik 'Antwoord analyseren'.";
        }
    }

    private async void AskAll_Click(object sender, RoutedEventArgs e)
    {
        var q = _viewModel.Question?.Trim();
        if (string.IsNullOrEmpty(q)) return;

        int n = 0;
        foreach (var (item, site) in _sites.Where(s => s.Value.Provider.QueryUrlTemplate is not null).ToList())
        {
            await NavigateSiteAsync(item, AiCatalog.UrlFor(site.Provider, q));
            n++;
        }
        _viewModel.StatusText = $"Vraag geopend in {n} sites (zonder portfolio-context). Wissel van tabblad om de antwoorden te vergelijken.";
        if (CurrentSiteItem is null) ProviderPivot.SelectedIndex = 1;
    }

    private static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    // ── Analyseren en acties ─────────────────────────────────────────────────

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSiteItem is not { } item) return;
        var view = await EnsureSiteAsync(item);
        if (view?.CoreWebView2 is null) return;

        // Selectie van de gebruiker, anders het laatste stuk van de pagina (daar staat het nieuwste antwoord).
        const string script =
            "(function(){var s=(window.getSelection()||'').toString();" +
            "if(s&&s.trim().length>20)return s;" +
            "var t=(document.body&&document.body.innerText)||'';return t.slice(-8000);})()";
        try
        {
            var json = await view.CoreWebView2.ExecuteScriptAsync(script);
            var text = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            _viewModel.AnalyzeWebText(text, _sites[item].Provider.Name);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Tekst van {Site} lezen mislukt", _sites[item].Provider.Name);
            _viewModel.StatusText = "Kon de tekst van de site niet lezen.";
        }
    }

    private async void Action_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SmartAction action)
            await _viewModel.ExecuteActionCommand.ExecuteAsync(action);
    }

    private void QuickPrompt_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AiQuickPrompt prompt)
        {
            _viewModel.ApplyQuickPromptCommand.Execute(prompt);
            QuestionBox.Focus(FocusState.Programmatic);
            QuestionBox.SelectionStart = QuestionBox.Text.Length;
        }
    }

    private void Recent_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string q)
        {
            _viewModel.Question = q;
            QuestionBox.Focus(FocusState.Programmatic);
        }
    }

    private async void Keys_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AiKeysDialog(_viewModel.Service, _viewModel.AppSettingsPublic) { XamlRoot = XamlRoot };
        try
        {
            if (await App.ShowContentDialogAsync(dialog) == ContentDialogResult.Primary)
                _viewModel.RefreshProviders();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Sleutelvenster kon niet worden geopend");
        }
    }
}
