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
        _viewModel.ViewLoading();
    }

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Terminate();
        Current = null;
    }
}
