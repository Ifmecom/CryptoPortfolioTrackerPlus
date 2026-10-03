using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Views;

public sealed partial class ThreePctView : Page
{
    public readonly ThreePctViewModel _viewModel;
    public static ThreePctView? Current { get; private set; }

    public ThreePctView(ThreePctViewModel viewModel)
    {
        Current     = this;
        _viewModel  = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        using (PerfLog.Measure("ThreePctView.ViewLoading")) _viewModel.ViewLoading();
    }

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Terminate();
        Current = null;
    }

    // ── TradingView (v1.48) ───────────────────────────────────────────────────

    private static ElementTheme Theme => App.Container.GetRequiredService<Settings>().AppTheme;

    private async void TvOpenRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ThreePctLiveRow row)
            await _viewModel.OpenTradingViewCommand.ExecuteAsync(row);
    }

    private async void TvPineRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not ThreePctLiveRow row) return;
        var setup = _viewModel.PineSetupFor(row);
        if (setup is null) { _viewModel.LiveScanStatus = $"{row.Symbol}: geen volledige setup (entry/SL/TP) — geen Pine Script."; return; }
        await Dialogs.PineScriptDialog.ShowAsync(XamlRoot, $"{setup.Name} ({setup.Direction})",
            PineScriptGenerator.ForSetup(setup, DateTime.Now), setup.Ticker,
            TradingViewSymbol.PairOf(setup.Ticker), Theme);
    }

    private async void TvPineTop_Click(object sender, RoutedEventArgs e)
    {
        var setups = _viewModel.TopPineSetups();
        if (setups.Count == 0) { _viewModel.LiveScanStatus = "Geen setups — start eerst een scan."; return; }
        await Dialogs.PineScriptDialog.ShowAsync(XamlRoot, $"3% Trading top-setups ({setups.Count})",
            PineScriptGenerator.ForWatchlist(setups, "CPT 3% top-setups", DateTime.Now), null,
            "3pct_top", Theme);
    }

    private async void TvWatchlist_Click(object sender, RoutedEventArgs e)
    {
        var (content, count) = _viewModel.BuildTradingViewWatchlist();
        if (count == 0) { _viewModel.LiveScanStatus = "Niets te exporteren — start eerst een scan."; return; }
        await Dialogs.PineScriptDialog.ExportWatchlistAsync(XamlRoot, "3pct", content, count, Theme);
    }
}
