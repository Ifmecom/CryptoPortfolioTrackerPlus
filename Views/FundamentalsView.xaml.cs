using CryptoPortfolioTracker.Dialogs;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Views;

public sealed partial class FundamentalsView : Page
{
    public readonly FundamentalsViewModel _viewModel;
    public static FundamentalsView? Current { get; private set; }

    public FundamentalsView(FundamentalsViewModel viewModel)
    {
        Current    = this;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
        => await _viewModel.ViewLoadingAsync();

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Terminate();
        Current = null;
    }

    private async void Detail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FundamentalRow row } || row.Data is null) return;

        var dialog = new FundamentalsDetailDialog(row.Data, _viewModel.AppSettingsPublic)
        {
            XamlRoot = XamlRoot,
        };
        await App.ShowContentDialogAsync(dialog);

        if (dialog.Saved)
            await _viewModel.SaveDueDiligenceAsync(row);
    }
}
