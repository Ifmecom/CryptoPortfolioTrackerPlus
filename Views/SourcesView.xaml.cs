using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Views;

public sealed partial class SourcesView : Page
{
    public readonly SourcesViewModel _viewModel;
    public static SourcesView? Current { get; private set; }

    public SourcesView(SourcesViewModel viewModel)
    {
        Current = this;
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

    private void EditSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BronSource source)
            _viewModel.OpenEditPanelCommand.Execute(source);
    }

    private async void DeleteSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BronSource source)
            await _viewModel.DeleteSourceCommand.ExecuteAsync(source);
    }
}
