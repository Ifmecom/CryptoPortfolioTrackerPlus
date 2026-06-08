using CryptoPortfolioTracker.Dialogs;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Views;

public sealed partial class PatternTradingView : Page
{
    public readonly PatternTradingViewModel _viewModel;
    public static PatternTradingView? Current { get; private set; }

    private readonly Settings _appSettings;

    public PatternTradingView(PatternTradingViewModel viewModel, Settings appSettings)
    {
        Current      = this;
        _viewModel   = viewModel;
        _appSettings = appSettings;
        InitializeComponent();
        DataContext = _viewModel;

        // Paper trade dialog is opened from code-behind (needs XamlRoot)
        _viewModel.PaperTradeRequested += OnPaperTradeRequested;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.ViewLoading();
    }

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PaperTradeRequested -= OnPaperTradeRequested;
        _viewModel.Terminate();
        Current = null;
    }

    // ── Handbook button ───────────────────────────────────────────────────────

    private void ShowHandbook_Click(object sender, RoutedEventArgs e)
    {
        var window = new PatternHandbookWindow();
        window.Activate();
    }

    // ── Category filter button click handlers ─────────────────────────────────

    private void FilterAll_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute(PatternFilter.All);

    private void FilterHighest_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute(PatternFilter.HighestScore);

    private void FilterBreakout_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute(PatternFilter.NearBreakout);

    private void FilterBullish_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute(PatternFilter.BullishOnly);

    private void FilterBearish_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute(PatternFilter.BearishOnly);

    // ── Sort button click handlers ────────────────────────────────────────────

    private void SortScore_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetSortModeCommand.Execute("Score");

    private void SortChange_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetSortModeCommand.Execute("Change24h");

    private void SortBreakout_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetSortModeCommand.Execute("Breakout");

    // ── TF filter button click handlers ──────────────────────────────────────

    private void TfAll_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetTfFilterCommand.Execute("All");

    private void Tf1D_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetTfFilterCommand.Execute("1D");

    private void Tf4H_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetTfFilterCommand.Execute("4H");

    private void Tf1H_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetTfFilterCommand.Execute("1H");

    private void Tf15M_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetTfFilterCommand.Execute("15M");

    // ── In-list search ────────────────────────────────────────────────────────

    private void ListSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
            _viewModel.HandleListSearch(tb.Text);
    }

    // ── Chart button inside DataTemplate ─────────────────────────────────────

    private void ShowChart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PatternCoinRow row)
        {
            var window = new CoinChartWindow(row.Analysis);
            window.Activate();
        }
    }

    // ── Pattern badge click → chart with annotation ───────────────────────────

    private void PatternBadge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PatternBadge badge)
        {
            var window = new CoinChartWindow(badge.Analysis, badge.Pattern);
            window.Activate();
        }
    }

    // ── Share / copy button inside DataTemplate ───────────────────────────────

    private async void CopyShare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PatternCoinRow row)
            await _viewModel.CopyShareTextCommand.ExecuteAsync(row);
    }

    // ── Watchlist remove button inside coin card ──────────────────────────────

    private async void RemoveWatchlist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PatternCoinRow row)
            await _viewModel.RemoveFromWatchlistCommand.ExecuteAsync(row);
    }

    // ── Watchlist panel — remove item chip ───────────────────────────────────

    private async void RemoveWatchlistItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchlistItem item)
            await _viewModel.RemoveWatchlistItemCommand.ExecuteAsync(item);
    }

    // ── AutoSuggestBox — search text changed ─────────────────────────────────

    private void SearchBox_TextChanged(AutoSuggestBox sender,
                                       AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            _viewModel.HandleSearchInput(sender.Text);
    }

    // ── AutoSuggestBox — Enter key or magnifying-glass button ────────────────
    // SuggestionChosen fires before QuerySubmitted when the user clicks a suggestion.
    // Let SuggestionChosen handle the add; only use QuerySubmitted for free-text searches.

    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender,
                                                AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // Suggestion-click is already handled by SuggestionChosen — skip to avoid double-add.
        if (args.ChosenSuggestion is WatchlistSearchResult)
            return;

        // Free-text Enter / magnifying-glass click → search immediately (bypass debounce).
        var query = args.QueryText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query))
            await _viewModel.SearchImmediateAsync(query);
    }

    // ── AutoSuggestBox — suggestion chosen → add to watchlist ────────────────

    private async void SearchBox_SuggestionChosen(AutoSuggestBox sender,
                                                   AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is WatchlistSearchResult result)
        {
            // Clear text and results first so the dropdown closes cleanly.
            sender.Text = string.Empty;
            _viewModel.SearchResults.Clear();
            await _viewModel.AddToWatchlistCommand.ExecuteAsync(result);
        }
    }

    // ── "Volg trade setup" button ─────────────────────────────────────────────

    private async void WatchSetup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PatternCoinRow row)
            await _viewModel.WatchSetupCommand.ExecuteAsync(row);
    }

    // ── "Neem papertrade" button ──────────────────────────────────────────────

    private void TakePaperTrade_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PatternCoinRow row)
            _viewModel.TakePaperTradeCommand.Execute(row);
    }

    // ── PaperTradeDialog opener (called from ViewModel event) ─────────────────

    private async void OnPaperTradeRequested(PatternCoinRow row)
    {
        if (row.Analysis?.Setup is null) return;

        // Look up the active WatchedSetup for this coin+direction BEFORE showing the dialog,
        // so we can link the resulting order back to the setup that triggered it.
        var watchedSetupService = App.Container.GetService<IWatchedSetupService>();
        WatchedSetup? activeSetup = null;
        if (watchedSetupService != null)
        {
            activeSetup = await watchedSetupService.GetActiveSetupForCoinAsync(
                row.Analysis.Coin.ApiId ?? string.Empty,
                row.Analysis.Setup.Direction);
        }

        var dialog = new PaperTradeDialog(row.Analysis.Coin, row.Analysis.Setup, _appSettings);
        dialog.XamlRoot = XamlRoot;
        await App.ShowContentDialogAsync(dialog);

        if (!dialog.Confirmed) return;

        var req = dialog.BuildOrderRequest();
        if (req is null) return;

        // Inject the WatchedSetupId so TradeService stores it on the ExchangeOrder
        if (activeSetup != null)
            req = req with { WatchedSetupId = activeSetup.Id };

        try
        {
            var tradeService = App.Container.GetService<ITradeService>();
            if (tradeService is null) return;

            var signal = new Signal
            {
                CoinId    = row.Analysis.Coin.Id,
                CreatedAt = DateTime.UtcNow,
                Direction = row.Analysis.Setup.Direction == "Short"
                    ? Enums.SignalDirection.Short
                    : Enums.SignalDirection.Long,
                Reasoning = $"Pattern Trading — {row.Analysis.Setup.Direction} (Score {row.Analysis.TradabilityScore})",
            };

            var order = await tradeService.PlacePaperAsync(row.Analysis.Coin, signal, req);

            // Set the reverse link: WatchedSetup.LinkedOrderId → ExchangeOrder.Id
            if (activeSetup != null && watchedSetupService != null)
                await watchedSetupService.LinkOrderAsync(activeSetup.Id, order.Id);

            _viewModel.StatusText = $"✓ Paper {req.Side} order geplaatst voor {row.Symbol} — {req.AmountUsdt:F0} USDT.";
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Paper trade mislukt: {ex.Message}";
        }
    }
}
