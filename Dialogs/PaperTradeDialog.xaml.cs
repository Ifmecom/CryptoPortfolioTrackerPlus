using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CryptoPortfolioTracker.Dialogs;

public sealed partial class PaperTradeDialog : ContentDialog
{
    private readonly CoinSignalRow? _row;
    private readonly Settings       _settings;

    // Visibility binding helper (Expander shows only when reasoning exists)
    private readonly Visibility _hasReasoning;

    /// <summary>Constructor used from SignalsView — takes a CoinSignalRow from the signal engine.</summary>
    public PaperTradeDialog(CoinSignalRow row, Settings settings)
    {
        _row          = row;
        _settings     = settings;
        _hasReasoning = !string.IsNullOrWhiteSpace(row.Reasoning)
            ? Visibility.Visible
            : Visibility.Collapsed;

        InitializeComponent();

        Title             = $"Paper Trade — {row.Name}";
        PrimaryButtonText = "Place Order";
        CloseButtonText   = "Cancel";

        txtSymbol.Text = row.Symbol;
        txtName.Text   = row.Name;
        txtPrice.Text  = row.Price > 0 ? $"${row.Price:#,0.########}" : "price unavailable";

        if (row.Direction == "Short")
        {
            rdBuy.IsChecked  = false;
            rdSell.IsChecked = true;
        }

        if (_hasReasoning == Visibility.Visible)
            txtReasoning.Text = row.Reasoning;
    }

    /// <summary>Constructor used from TradeAnalysisView — takes coin + setup advice directly,
    /// pre-filling SL%, TP% and direction from the trade setup.</summary>
    public PaperTradeDialog(Coin coin, TradeSetupAdvice setup, Settings settings)
    {
        _row      = null;
        _settings = settings;

        var reasoningText = setup.Reasoning.Any()
            ? string.Join("\n", setup.Reasoning)
            : string.Empty;

        _hasReasoning = !string.IsNullOrWhiteSpace(reasoningText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        InitializeComponent();

        Title             = $"Paper Trade — {coin.Name}";
        PrimaryButtonText = "Place Order";
        CloseButtonText   = "Cancel";

        txtSymbol.Text = coin.Symbol?.ToUpperInvariant() ?? string.Empty;
        txtName.Text   = coin.Name;
        txtPrice.Text  = coin.Price > 0 ? $"${coin.Price:#,0.########}" : "price unavailable";

        // Pre-fill side from trade setup direction
        if (setup.Direction == "Short")
        {
            rdBuy.IsChecked  = false;
            rdSell.IsChecked = true;
        }

        // Pre-fill SL% and TP% (Target 1) from the setup advice
        if (setup.StopLossPct > 0)
            nbStopLoss.Value = Math.Round(setup.StopLossPct, 1);
        if (setup.Target1Pct > 0)
            nbTakeProfit.Value = Math.Round(setup.Target1Pct, 1);

        if (_hasReasoning == Visibility.Visible)
            txtReasoning.Text = reasoningText;
    }

    private void Dialog_Loading(FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _settings.AppTheme)
            sender.RequestedTheme = _settings.AppTheme;
    }

    /// <summary>
    /// Returns an <see cref="OrderRequest"/> from the dialog inputs, or null if validation fails.
    /// </summary>
    public OrderRequest? BuildOrderRequest()
    {
        var exchange = cmbExchange.SelectedItem is ComboBoxItem item
            ? (item.Tag?.ToString() == "Mexc" ? ExchangeKind.Mexc : ExchangeKind.Bybit)
            : ExchangeKind.Bybit;

        var side   = rdSell.IsChecked == true ? OrderSide.Sell : OrderSide.Buy;
        var amount = double.IsNaN(nbAmount.Value)    ? 100 : nbAmount.Value;
        var sl     = double.IsNaN(nbStopLoss.Value)  ? 5   : nbStopLoss.Value;
        var tp     = double.IsNaN(nbTakeProfit.Value) ? 10  : nbTakeProfit.Value;

        if (amount <= 0) return null;

        return new OrderRequest(exchange, side, amount, sl, tp);
    }
}
