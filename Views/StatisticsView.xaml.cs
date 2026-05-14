using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Views;

public sealed partial class StatisticsView : Page
{
    public readonly StatisticsViewModel _viewModel;
    public static StatisticsView? Current { get; private set; }

    public StatisticsView(StatisticsViewModel viewModel)
    {
        Current    = this;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.ViewLoading();
    }

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Terminate();
        Current = null;
    }
}
