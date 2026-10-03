using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CryptoPortfolioTracker.Views;

public sealed partial class SetupTrackerView : Page
{
    private readonly SetupTrackerViewModel _viewModel;

    public SetupTrackerView(SetupTrackerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
        { using (PerfLog.Measure("SetupTrackerView.ViewLoadingAsync")) await _viewModel.ViewLoadingAsync(); }

    private void View_Unloaded(object sender, RoutedEventArgs e)
        => _viewModel.Terminate();

    // ── Filter clicks ─────────────────────────────────────────────────────────

    private void FilterAll_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute("All");

    private void FilterWatching_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute("Watching");

    private void FilterOpen_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute("Open");

    private void FilterWon_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute("Won");

    private void FilterLost_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute("Lost");

    private void FilterExpired_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetFilterCommand.Execute("Expired");

    // ── Row action buttons ────────────────────────────────────────────────────

    // ── TradingView (v1.48) ───────────────────────────────────────────────────

    private static ElementTheme Theme => App.Container.GetRequiredService<Settings>().AppTheme;

    private async void TvOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WatchedSetup setup })
            await _viewModel.OpenTradingViewCommand.ExecuteAsync(setup);
    }

    private async void TvPine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: WatchedSetup ws }) return;
        var setup = _viewModel.PineSetupFor(ws);
        if (setup is null) { _viewModel.StatusText = $"{ws.CoinSymbol}: onvolledige setup — geen Pine Script."; return; }
        await Dialogs.PineScriptDialog.ShowAsync(XamlRoot, $"{setup.Name} ({setup.Direction})",
            PineScriptGenerator.ForSetup(setup, DateTime.Now), setup.Ticker,
            TradingViewSymbol.PairOf(setup.Ticker), Theme);
    }

    private async void TvPineTop_Click(object sender, RoutedEventArgs e)
    {
        var setups = _viewModel.TopPineSetups();
        if (setups.Count == 0) { _viewModel.StatusText = "Geen lopende setups om naar TradingView te sturen."; return; }
        await Dialogs.PineScriptDialog.ShowAsync(XamlRoot, $"Setup Tracker ({setups.Count})",
            PineScriptGenerator.ForWatchlist(setups, "CPT Setup Tracker", DateTime.Now), null,
            "tracker", Theme);
    }

    private async void TvWatchlist_Click(object sender, RoutedEventArgs e)
    {
        var (content, count) = _viewModel.BuildTradingViewWatchlist();
        if (count == 0) { _viewModel.StatusText = "Geen lopende setups om te exporteren."; return; }
        await Dialogs.PineScriptDialog.ExportWatchlistAsync(XamlRoot, "tracker", content, count, Theme);
    }

    private async void CloseAsWon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WatchedSetup setup }) return;

        // Waarschuw als TP1 nog niet bereikt is
        bool tp1Reached = setup.CurrentPrice > 0 && setup.Target1 > 0 &&
                          (setup.Direction == "Short"
                              ? setup.CurrentPrice <= setup.Target1
                              : setup.CurrentPrice >= setup.Target1);

        if (!tp1Reached && setup.CurrentPrice > 0 && setup.Target1 > 0)
        {
            double distPct = setup.Direction == "Short"
                ? (setup.CurrentPrice - setup.Target1) / setup.Target1  * 100
                : (setup.Target1      - setup.CurrentPrice) / setup.CurrentPrice * 100;

            string curStr = FormatPrice(setup.CurrentPrice);
            string tp1Str = FormatPrice(setup.Target1);

            var dialog = new ContentDialog
            {
                Title             = $"⚠️  TP1 nog niet bereikt — {setup.CoinSymbol}",
                Content           = $"De huidige koers ({curStr}) heeft TP1 ({tp1Str}) nog niet bereikt.\n" +
                                    $"Afstand tot TP1: nog {distPct:F1}% te gaan.\n\n" +
                                    $"Als je nu als Gewonnen markeert, wordt de setup gesloten " +
                                    $"op de huidige koers ({curStr}) — niet op TP1.",
                PrimaryButtonText = "Toch als Gewonnen markeren",
                CloseButtonText   = "Annuleren",
                DefaultButton     = ContentDialogButton.Close,
                XamlRoot          = XamlRoot,
            };

            var result = await App.ShowContentDialogAsync(dialog);
            if (result != ContentDialogResult.Primary) return;
        }

        _viewModel.CloseAsWonCommand.Execute(setup);
    }

    private void CloseAsLost_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WatchedSetup setup })
            _viewModel.CloseAsLostCommand.Execute(setup);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatPrice(double price) =>
        price >= 10_000 ? $"${price:N0}"
      : price >= 1_000  ? $"${price:N1}"
      : price >= 100    ? $"${price:N2}"
      : price >= 10     ? $"${price:N3}"
      : price >= 1      ? $"${price:N4}"
      : price >= 0.01   ? $"${price:N5}"
      :                    $"${price:N6}";

    private void Expire_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WatchedSetup setup })
            _viewModel.ExpireCommand.Execute(setup);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WatchedSetup setup })
            _viewModel.RemoveCommand.Execute(setup);
    }
}
