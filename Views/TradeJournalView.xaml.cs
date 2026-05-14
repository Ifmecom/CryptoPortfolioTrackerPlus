using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Views;

public sealed partial class TradeJournalView : Page
{
    public readonly TradeJournalViewModel _viewModel;
    public static TradeJournalView? Current { get; private set; }

    public TradeJournalView(TradeJournalViewModel viewModel)
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

    private async void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TradeJournalRow row)
            await _viewModel.CancelOrderCommand.ExecuteAsync(row);
    }

    private async void ClosePosition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TradeJournalRow row)
            await _viewModel.CloseOrderCommand.ExecuteAsync(row);
    }

    private async void EditNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TradeJournalRow row) return;

        // Build dialog with a TextBox pre-filled with the current note
        var textBox = new TextBox
        {
            Text              = row.Notes,
            PlaceholderText   = "Voeg een notitie toe…",
            AcceptsReturn     = true,
            TextWrapping      = TextWrapping.Wrap,
            MinHeight         = 80,
            MaxWidth          = 400,
        };

        var dialog = new ContentDialog
        {
            Title               = $"Notitie — {row.Symbol}",
            Content             = textBox,
            PrimaryButtonText   = "Opslaan",
            CloseButtonText     = "Annuleren",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
        };

        var result = await App.ShowContentDialogAsync(dialog);
        if (result != ContentDialogResult.Primary) return;

        await _viewModel.SaveNoteAsync(row.Id, textBox.Text.Trim());
    }
}
