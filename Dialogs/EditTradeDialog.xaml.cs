using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Dialogs;

/// <summary>
/// Dialoog voor het aanpassen van SL / TP1 / TP2 van een lopende paper trade.
/// </summary>
public sealed partial class EditTradeDialog : ContentDialog
{
    // ── State ────────────────────────────────────────────────────────────────
    private readonly Settings      _settings;
    private readonly ExchangeOrder _order;
    private readonly double        _currentPrice;
    private bool                   _initialising = true;

    // Derived (read-only after init)
    private readonly bool   _isLong;
    private readonly double _initialRisk; // |entry − originalSL| per unit

    // ── Result ───────────────────────────────────────────────────────────────
    public bool   Confirmed    { get; private set; }
    public double NewStopLoss  { get; private set; }
    public double NewTakeProfit  { get; private set; }
    public double NewTakeProfit2 { get; private set; }

    // ── Brushes ──────────────────────────────────────────────────────────────
    private static readonly SolidColorBrush GreenBrush   = new(Color.FromArgb(255, 76, 175, 80));
    private static readonly SolidColorBrush RedBrush     = new(Color.FromArgb(255, 229, 57, 53));
    private static readonly SolidColorBrush OrangeBrush  = new(Color.FromArgb(255, 255, 167, 38));
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromArgb(255, 158, 158, 158));

    // ── Constructor ──────────────────────────────────────────────────────────

    public EditTradeDialog(ExchangeOrder order, double currentPrice, Settings settings)
    {
        _settings     = settings;
        _order        = order;
        _currentPrice = currentPrice;
        _isLong       = order.Side == OrderSide.Buy;
        _initialRisk  = order.StopLoss > 0
            ? Math.Abs(order.Entry - order.StopLoss)
            : 0;

        InitializeComponent();
        Populate();
    }

    // ── Initialisation ───────────────────────────────────────────────────────

    private void Populate()
    {
        _initialising = true;

        // Banner
        txtSymbol.Text  = _order.Symbol;
        txtEntry.Text   = $"$ {FormatPrice(_order.Entry)}";
        txtCurrent.Text = _currentPrice > 0 ? $"$ {FormatPrice(_currentPrice)}" : "—";
        txtSide.Text    = _isLong ? "Long ▲" : "Short ▼";

        if (_isLong)
        {
            bdgSide.Background = new SolidColorBrush(Color.FromArgb(50, 76, 175, 80));
            txtSide.Foreground = GreenBrush;
        }
        else
        {
            bdgSide.Background = new SolidColorBrush(Color.FromArgb(50, 229, 57, 53));
            txtSide.Foreground = RedBrush;
        }

        // P&L
        if (_currentPrice > 0 && _order.Entry > 0 && _order.Qty > 0)
        {
            var pnl = _isLong
                ? Math.Round((_currentPrice - _order.Entry) * _order.Qty, 2)
                : Math.Round((_order.Entry - _currentPrice) * _order.Qty, 2);
            var pct = _isLong
                ? (_currentPrice - _order.Entry) / _order.Entry * 100
                : (_order.Entry - _currentPrice) / _order.Entry * 100;

            txtPnl.Text       = $"{pnl:+0.00;-0.00} USDT";
            txtPnlPct.Text    = $"{pct:+0.0;-0.0} %";
            txtPnl.Foreground = pnl >= 0 ? GreenBrush : RedBrush;
        }

        // Current SL / TP labels
        txtCurrentSL.Text  = _order.StopLoss > 0   ? FormatPrice(_order.StopLoss)   : "—";
        txtCurrentTP1.Text = _order.TakeProfit > 0  ? FormatPrice(_order.TakeProfit) : "—";
        txtCurrentTP2.Text = _order.TakeProfit2 > 0 ? FormatPrice(_order.TakeProfit2): "—";

        SetPctLabel(txtCurrentSLPct,  _order.StopLoss,    _order.Entry, isLoss: true);
        SetPctLabel(txtCurrentTP1Pct, _order.TakeProfit,  _order.Entry, isLoss: false);
        SetPctLabel(txtCurrentTP2Pct, _order.TakeProfit2, _order.Entry, isLoss: false);

        // Preset-knop tooltips: toon berekende prijs erbij als we risico kennen
        if (_initialRisk > 0)
        {
            var be    = _order.Entry;
            var halfR = _isLong ? _order.Entry + _initialRisk * 0.5 : _order.Entry - _initialRisk * 0.5;
            var oneR  = _isLong ? _order.Entry + _initialRisk       : _order.Entry - _initialRisk;
            ToolTipService.SetToolTip(btnBreakeven,
                $"Breakeven — SL naar {FormatPrice(be)} (geen verlies meer mogelijk)");
            ToolTipService.SetToolTip(btnHalfR,
                $"½R vrij — SL naar {FormatPrice(halfR)} (+½ initieel risico geborgd)");
            ToolTipService.SetToolTip(btnOneR,
                $"+1R — SL naar {FormatPrice(oneR)} (1R winst gegarandeerd)");
        }
        else
        {
            // No initial SL → disable presets that depend on risk
            btnHalfR.IsEnabled = false;
            btnOneR.IsEnabled  = false;
        }

        // Pre-fill NumberBoxes with current values
        if (_order.StopLoss   > 0) nbSL.Value  = _order.StopLoss;
        if (_order.TakeProfit > 0) nbTP1.Value = _order.TakeProfit;
        if (_order.TakeProfit2> 0) nbTP2.Value = _order.TakeProfit2;

        _initialising = false;
        RecalcSummary();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void Dialog_Loading(FrameworkElement sender, object args)
    {
        if (sender.ActualTheme != _settings.AppTheme)
            sender.RequestedTheme = _settings.AppTheme;
    }

    private void OnValueChanged(NumberBox _, NumberBoxValueChangedEventArgs __) => RecalcSummary();

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        double newSL = btn.Tag?.ToString() switch
        {
            "breakeven" => _order.Entry,
            "halfr"     => _isLong
                               ? _order.Entry + _initialRisk * 0.5
                               : _order.Entry - _initialRisk * 0.5,
            "oner"      => _isLong
                               ? _order.Entry + _initialRisk
                               : _order.Entry - _initialRisk,
            _           => double.NaN,
        };

        if (!double.IsNaN(newSL) && newSL > 0)
        {
            nbSL.Value = Math.Round(newSL, GetDecimals(newSL));

            // Flash-highlight the active preset button
            HighlightPreset(btn.Tag?.ToString());
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        NewStopLoss   = SafeValue(nbSL,  0);
        NewTakeProfit  = SafeValue(nbTP1, 0);
        NewTakeProfit2 = SafeValue(nbTP2, 0);
        Confirmed = true;
        Hide();
    }

    // ── Recalculate summary bar ───────────────────────────────────────────────

    private void RecalcSummary()
    {
        if (_initialising) return;

        var sl  = SafeValue(nbSL,  0);
        var tp1 = SafeValue(nbTP1, 0);
        var entry = _order.Entry;

        // Live %-labels for new values
        SetPctLabel(txtNewSLPct,  sl,  entry, isLoss: true);
        SetPctLabel(txtNewTP1Pct, tp1, entry, isLoss: false);
        SetPctLabel(txtNewTP2Pct, SafeValue(nbTP2, 0), entry, isLoss: false);

        // R/R ratio
        if (sl > 0 && tp1 > 0 && entry > 0)
        {
            var risk   = Math.Abs(entry - sl);
            var reward = Math.Abs(tp1 - entry);
            if (risk > 0)
            {
                var rr = reward / risk;
                txtRR.Text       = $"{rr:F2} : 1";
                txtRR.Foreground = rr >= 1.5 ? GreenBrush : rr >= 1.0 ? OrangeBrush : RedBrush;
            }
            else { txtRR.Text = "—"; txtRR.Foreground = NeutralBrush; }
        }
        else { txtRR.Text = "—"; txtRR.Foreground = NeutralBrush; }

        // Max risico (in USDT, based on current qty)
        if (sl > 0 && entry > 0 && _order.Qty > 0)
        {
            var riskUsdt = Math.Abs(entry - sl) * _order.Qty;
            txtMaxRisk.Text       = $"{riskUsdt:N2} USDT";
            txtMaxRisk.Foreground = riskUsdt > 0 ? RedBrush : NeutralBrush;
        }
        else { txtMaxRisk.Text = "—"; txtMaxRisk.Foreground = NeutralBrush; }

        // SL afstand van entry
        if (sl > 0 && entry > 0)
        {
            var dist = Math.Abs(entry - sl) / entry * 100;
            txtSlDistance.Text       = $"{dist:F2}%";
            txtSlDistance.Foreground = NeutralBrush;
        }
        else { txtSlDistance.Text = "—"; txtSlDistance.Foreground = NeutralBrush; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetPctLabel(TextBlock lbl, double price, double entry, bool isLoss)
    {
        if (price <= 0 || entry <= 0) { lbl.Text = string.Empty; return; }
        var pct = (price - entry) / entry * 100;
        lbl.Text       = $"{pct:+0.00;-0.00}%";
        lbl.Foreground = pct < 0
            ? (isLoss ? NeutralBrush : RedBrush)
            : (isLoss ? RedBrush     : GreenBrush);
    }

    private void HighlightPreset(string? tag)
    {
        btnBreakeven.Opacity = tag == "breakeven" ? 1.0 : 0.6;
        btnHalfR.Opacity     = tag == "halfr"     ? 1.0 : 0.6;
        btnOneR.Opacity      = tag == "oner"       ? 1.0 : 0.6;
    }

    private static double SafeValue(NumberBox nb, double fallback)
        => double.IsNaN(nb.Value) || nb.Value < 0 ? fallback : nb.Value;

    private static string FormatPrice(double price) => price switch
    {
        >= 1000  => $"{price:#,0.00}",
        >= 1     => $"{price:F4}",
        >= 0.01  => $"{price:F6}",
        _        => $"{price:F8}",
    };

    private static int GetDecimals(double price) => price switch
    {
        >= 1000 => 2,
        >= 1    => 4,
        >= 0.01 => 6,
        _       => 8,
    };
}
