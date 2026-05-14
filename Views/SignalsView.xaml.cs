using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CryptoPortfolioTracker.Views;

public sealed partial class SignalsView : Page
{
    public readonly SignalsViewModel _viewModel;
    public static SignalsView? Current { get; private set; }

    private ScrollViewer? _listScrollViewer;

    public SignalsView(SignalsViewModel viewModel)
    {
        Current = this;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.ViewLoading();

        // Hook up horizontal scroll sync after the ListView has rendered
        DataListView.Loaded += OnListViewLoaded;
    }

    private void OnListViewLoaded(object sender, RoutedEventArgs e)
    {
        _listScrollViewer = FindScrollViewer(DataListView);
        if (_listScrollViewer is not null)
            _listScrollViewer.ViewChanged += OnListScrollViewerChanged;
    }

    private void OnListScrollViewerChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        // Keep the sticky header in sync with the list's horizontal scroll position
        if (_listScrollViewer is not null)
            HeaderScrollViewer.ChangeView(_listScrollViewer.HorizontalOffset, null, null, true);
    }

    private void View_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_listScrollViewer is not null)
            _listScrollViewer.ViewChanged -= OnListScrollViewerChanged;

        _viewModel.Terminate();
        Current = null;
    }

    // Forward row click to ViewModel command
    // (using Tag+Click because x:Bind commands from parent VM inside DataTemplate
    //  require x:Name on Page root, which we intentionally avoid — see CLAUDE.md)
    private async void PaperTrade_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CoinSignalRow row)
            await _viewModel.PlacePaperTradeCommand.ExecuteAsync(row);
    }

    // Walk the visual tree to find the first ScrollViewer inside a given element
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }
}
