using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using CryptoPortfolioTracker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CryptoPortfolioTracker.Dialogs;

public sealed partial class PaperTradeDialog : ContentDialog
{
    // ── State ────────────────────────────────────────────────────────────────
    private readonly Settings _settings;
    private double  _currentPrice;
    private string  _symbolBase   = string.Empty;  // e.g. "BTC"
    private bool    _initialising = true;           // suppress recalc during setup
    private double  _capital      = 10_000.0;        // kapitaalbasis (paper of echt) — gezet in Dialog_Loading
    private string  _capitalBasis = "virtueel paper-kapitaal";

    // TP close percentages (% of total position to close at each TP level)
    private double _tp1ClosePct = 50.0;
    private double _tp2ClosePct = 50.0;

    // ── Public result ────────────────────────────────────────────────────────
    /// <summary>True when the user clicked Open Long or Open Short (not Annuleren).</summary>
    public bool      Confirmed    { get; private set; }
    public OrderSide SelectedSide { get; private set; } = OrderSide.Buy;

    // ── Shared brushes ───────────────────────────────────────────────────────
    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255, 76, 175, 80));
    private static readonly SolidColorBrush RedBrush   = new(Color.FromArgb(255, 229, 57, 53));
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromArgb(255, 158, 158, 158));

    // ────────────────────────────────────────────────────────────────────────
    // Constructors
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>From SignalsView — takes a CoinSignalRow from the signal engine.</summary>
    public PaperTradeDialog(CoinSignalRow row, Settings settings)
    {
        _settings = settings;
        // Set theme BEFORE InitializeComponent so the initial template application
        // already uses the correct theme — avoids a re-template mid-show that can
        // cause "Failed to assign to property 'RangeBase.Minimum'" on some SDK builds.
        this.RequestedTheme = settings.AppTheme;
        InitializeComponent();

        var reasoning = row.Reasoning ?? string.Empty;
        Initialize(
            symbol:    row.Symbol,
            name:      row.Name,
            price:     row.Price,
            change24h: 0,
            direction: row.Direction,
            slPrice:   0,
            tp1Price:  0,
            tp2Price:  0,
            reasoning: reasoning);
    }

    /// <summary>From TradeAnalysisView — takes Coin + TradeSetupAdvice (pre-filled SL/TP).</summary>
    public PaperTradeDialog(Coin coin, TradeSetupAdvice setup, Settings settings)
    {
        _settings = settings;
        // Same pre-InitializeComponent theme trick as the other constructor.
        this.RequestedTheme = settings.AppTheme;
        InitializeComponent();

        var reasoning = setup.Reasoning.Any()
            ? string.Join("\n", setup.Reasoning)
            : string.Empty;

        Initialize(
            symbol:    coin.Symbol?.ToUpperInvariant() ?? string.Empty,
            name:      coin.Name,
            price:     coin.Price,
            change24h: coin.Change24Hr,
            direction: setup.Direction,
            slPrice:   setup.StopLoss,
            tp1Price:  setup.Target1,
            tp2Price:  setup.Target2,
            reasoning: reasoning);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Shared initialisation
    // ────────────────────────────────────────────────────────────────────────

    private void Initialize(
        string symbol, string name, double price, double change24h,
        string direction, double slPrice, double tp1Price, double tp2Price,
        string reasoning)
    {
        _initialising = true;
        _currentPrice = price;
        _symbolBase   = symbol.Replace("USDT", "").Replace("usdt", "").ToUpperInvariant();

        // ── Default radio / checkbox state (bool? needs code-behind in WinUI 3) ─
        rdSpot.IsChecked   = true;
        rdMarket.IsChecked = true;          // Market = direct gevuld; gebruiker kan switchen naar Limit
        pnlLimitPrice.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        chkSL.IsChecked   = true;
        chkTP1.IsChecked  = true;
        chkTP2.IsChecked  = false;

        // ── TP close-% defaults ──────────────────────────────────────────────
        // WinUI 3 rejects setting Minimum when Value < Minimum (throws RangeBase.Minimum).
        // Safe order: Maximum first (expands range), then Value (within range), then Minimum.
        _tp1ClosePct = 50.0;
        _tp2ClosePct = 50.0;
        sldTP1Close.Maximum = 100;
        sldTP1Close.Value   = _tp1ClosePct;
        sldTP1Close.Minimum = 1;
        sldTP2Close.Maximum = 100;
        sldTP2Close.Value   = _tp2ClosePct;
        sldTP2Close.Minimum = 1;
        UpdateTpClosePctLabels();

        // ── Coin banner ──────────────────────────────────────────────────────
        txtSymbol.Text       = $"{_symbolBase}/USDT";
        txtName.Text         = name;
        txtCurrentPrice.Text = price > 0 ? $"$ {FormatPrice(price)}" : "prijs niet beschikbaar";

        if (change24h != 0)
        {
            txtChange.Text       = $"{change24h:+0.00;-0.00}%";
            txtChange.Foreground = change24h >= 0 ? GreenBrush : RedBrush;
        }
        else
        {
            txtChange.Text = string.Empty;
        }

        // ── Apply price formatter to eliminate IEEE-754 display noise ───────
        // e.g. 73741.85 would otherwise show as "73741,849999999991"
        if (price > 0)
        {
            var fmt = MakePriceFormatter(price);
            nbLimitPrice.NumberFormatter = fmt;
            nbSL.NumberFormatter         = fmt;
            nbTP1.NumberFormatter        = fmt;
            nbTP2.NumberFormatter        = fmt;
        }

        // ── Limit price default = current price ──────────────────────────────
        if (price > 0)
            nbLimitPrice.Value = Math.Round(price, GetDecimals(price));

        // ── Pre-fill SL / TP ─────────────────────────────────────────────────
        if (slPrice > 0)
        {
            chkSL.IsChecked = true;
            nbSL.Value      = Math.Round(slPrice, GetDecimals(slPrice));
        }
        else if (price > 0)
        {
            // Default SL: −5% for Long, +5% for Short
            var defaultSL = direction == "Short"
                ? Math.Round(price * 1.05, GetDecimals(price))
                : Math.Round(price * 0.95, GetDecimals(price));
            nbSL.Value = defaultSL;
        }

        if (tp1Price > 0)
        {
            chkTP1.IsChecked = true;
            nbTP1.Value      = Math.Round(tp1Price, GetDecimals(tp1Price));
        }
        else if (price > 0)
        {
            // Default TP1: +10% for Long, −10% for Short
            var defaultTP1 = direction == "Short"
                ? Math.Round(price * 0.90, GetDecimals(price))
                : Math.Round(price * 1.10, GetDecimals(price));
            nbTP1.Value = defaultTP1;
        }

        if (tp2Price > 0)
        {
            chkTP2.IsChecked  = true;
            nbTP2.IsEnabled   = true;
            nbTP2.Value       = Math.Round(tp2Price, GetDecimals(tp2Price));
        }

        // ── Reasoning ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            txtReasoning.Text             = reasoning;
            reasoningExpander.Visibility  = Visibility.Visible;
        }

        // ── Available capital label ──────────────────────────────────────────
        txtAvailable.Text = $"Kapitaalbasis: {_capital:#,0} USDT ({_capitalBasis})";

        // ── Risico-sizing defaults + kill-switch waarschuwing (guardrails) ────
        nbRiskPct.Value     = _settings.MaxPortfolioPercPerTrade > 0 ? _settings.MaxPortfolioPercPerTrade : 2.0;
        killSwitchBar.IsOpen = _settings.IsKillSwitchActive;

        _initialising = false;
        Recalculate();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Event handlers — selectors
    // ────────────────────────────────────────────────────────────────────────

    private async void Dialog_Loading(FrameworkElement sender, object args)
    {
        // Bepaal de kapitaalbasis (paper vs echte portfolio) volgens de instelling.
        try
        {
            var capitalSvc = App.Container?.GetService<Services.IRiskCapitalService>();
            if (capitalSvc is not null)
            {
                _capital      = await capitalSvc.GetCapitalAsync();
                _capitalBasis = capitalSvc.BasisLabel;
                txtAvailable.Text = $"Kapitaalbasis: {_capital:#,0} USDT ({_capitalBasis})";
                Recalculate();
            }
        }
        catch { /* val terug op de standaard paper-waarde */ }
    }

    private void MarketType_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        var isFuturesOrMargin = rdFutures.IsChecked == true || rdMargin.IsChecked == true;
        pnlLeverage.Visibility = isFuturesOrMargin ? Visibility.Visible : Visibility.Collapsed;
        Recalculate();
    }

    private void OrderType_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        pnlLimitPrice.Visibility = rdLimit.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        Recalculate();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Event handlers — values
    // ────────────────────────────────────────────────────────────────────────

    private void OnLimitPriceChanged(NumberBox _, NumberBoxValueChangedEventArgs __) => Recalculate();
    private void OnAmountChanged(NumberBox _,     NumberBoxValueChangedEventArgs __) => Recalculate();
    private void OnLeverageChanged(object _,      SelectionChangedEventArgs __)      => Recalculate();
    private void OnSLTPChanged(NumberBox _,       NumberBoxValueChangedEventArgs __) => Recalculate();

    private void SLTP_Toggled(object sender, RoutedEventArgs e)
    {
        nbSL.IsEnabled  = chkSL.IsChecked  == true;
        nbTP1.IsEnabled = chkTP1.IsChecked == true;
        nbTP2.IsEnabled = chkTP2.IsChecked == true;

        // Show/hide close-% panels
        pnlTP1Close.Visibility = chkTP1.IsChecked == true ? Visibility.Visible   : Visibility.Collapsed;
        pnlTP2Close.Visibility = chkTP2.IsChecked == true ? Visibility.Visible   : Visibility.Collapsed;

        Recalculate();
    }

    private void QuickPct_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int pct))
            nbAmount.Value = Math.Round(_capital * pct / 100.0, 2);
    }

    /// <summary>Berekent het inlegbedrag zodat verlies-bij-SL = risico% van het kapitaal.</summary>
    private void RiskSize_Click(object sender, RoutedEventArgs e)
    {
        var entry   = EffectiveEntryPrice();
        var sl      = chkSL.IsChecked == true ? SafeValue(nbSL, 0) : 0;
        var riskPct = double.IsNaN(nbRiskPct.Value) ? 0 : nbRiskPct.Value;

        var res = PositionSizeCalculator.Suggest(_capital, riskPct, entry, sl, GetLeverage());
        if (!res.IsValid)
        {
            txtRiskInfo.Text       = "⚠ Stel eerst een geldige entry én stop-loss in.";
            txtRiskInfo.Foreground = RedBrush;
            return;
        }

        // Niet meer margin inleggen dan beschikbaar (bij krappe SL kan de suggestie groot zijn).
        var amount = Math.Min(res.Amount, _capital);
        nbAmount.Value = Math.Round(amount, 2);   // triggert Recalculate via OnAmountChanged
    }

    // ── TP1 close-% ─────────────────────────────────────────────────────────

    private void TP1ClosePct_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double pct))
        {
            _tp1ClosePct      = pct;
            sldTP1Close.Value = pct;
            UpdateTpClosePctLabels();
        }
    }

    private void OnTP1CloseSlider(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _tp1ClosePct = Math.Round(e.NewValue);
        UpdateTpClosePctLabels();
    }

    // ── TP2 close-% ─────────────────────────────────────────────────────────

    private void TP2ClosePct_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double pct))
        {
            _tp2ClosePct      = pct;
            sldTP2Close.Value = pct;
            UpdateTpClosePctLabels();
        }
    }

    private void OnTP2CloseSlider(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _tp2ClosePct = Math.Round(e.NewValue);
        UpdateTpClosePctLabels();
    }

    // ── Shared: update labels + highlight active quick-button ────────────────

    private void UpdateTpClosePctLabels()
    {
        txtTP1ClosePct.Text = $"{_tp1ClosePct:0}%";
        txtTP2ClosePct.Text = $"{_tp2ClosePct:0}%";

        HighlightClosePctButtons(
            btnTP1_25, btnTP1_50, btnTP1_75, btnTP1_100, _tp1ClosePct);
        HighlightClosePctButtons(
            btnTP2_25, btnTP2_50, btnTP2_75, btnTP2_100, _tp2ClosePct);
    }

    private static void HighlightClosePctButtons(Button b25, Button b50, Button b75, Button b100, double val)
    {
        b25.Style  = val == 25  ? null : null;   // keep default style; use Opacity to signal active
        b50.Style  = null;
        b75.Style  = null;
        b100.Style = null;

        // Dim inactive buttons, full opacity for the matching one
        b25.Opacity  = val == 25  ? 1.0 : 0.55;
        b50.Opacity  = val == 50  ? 1.0 : 0.55;
        b75.Opacity  = val == 75  ? 1.0 : 0.55;
        b100.Opacity = val == 100 ? 1.0 : 0.55;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Recalculate summary bar
    // ────────────────────────────────────────────────────────────────────────

    private void Recalculate()
    {
        if (_initialising) return;

        var entryPrice = EffectiveEntryPrice();
        var amount     = SafeValue(nbAmount, 100);
        var leverage   = GetLeverage();

        // Cost and quantity
        var effectiveAmount = amount * leverage;
        var qty = entryPrice > 0 ? effectiveAmount / entryPrice : 0;

        txtCost.Text = $"{amount:N2} USDT";
        txtQty.Text  = qty > 0 ? $"{FormatQty(qty)} {_symbolBase}" : "—";
        if (leverage > 1)
            txtCost.Text += $" (×{leverage})";

        // SL percentage and colour
        UpdatePctLabel(txtSLPct, chkSL, nbSL, entryPrice, isLoss: true);
        UpdatePctLabel(txtTP1Pct, chkTP1, nbTP1, entryPrice, isLoss: false);
        UpdatePctLabel(txtTP2Pct, chkTP2, nbTP2, entryPrice, isLoss: false);

        // R/R ratio
        if (chkSL.IsChecked == true && chkTP1.IsChecked == true)
        {
            var sl  = SafeValue(nbSL,  0);
            var tp1 = SafeValue(nbTP1, 0);
            if (sl > 0 && tp1 > 0 && entryPrice > 0)
            {
                var risk   = Math.Abs(entryPrice - sl);
                var reward = Math.Abs(tp1 - entryPrice);
                txtRR.Text       = risk > 0 ? $"{reward / risk:F2} : 1" : "—";
                txtRR.Foreground = reward >= risk ? GreenBrush : RedBrush;
            }
            else { txtRR.Text = "—"; txtRR.Foreground = NeutralBrush; }
        }
        else { txtRR.Text = "—"; txtRR.Foreground = NeutralBrush; }

        // Max risk in USDT
        if (chkSL.IsChecked == true && entryPrice > 0 && qty > 0)
        {
            var sl     = SafeValue(nbSL, 0);
            var maxLoss = sl > 0 ? Math.Abs(entryPrice - sl) * qty : 0;
            txtMaxRisk.Text       = maxLoss > 0 ? $"{maxLoss:N2} USDT" : "—";
            txtMaxRisk.Foreground = maxLoss > 0 ? RedBrush : NeutralBrush;
        }
        else { txtMaxRisk.Text = "—"; txtMaxRisk.Foreground = NeutralBrush; }

        // Leverage info label
        if (pnlLeverage.Visibility == Visibility.Visible)
        {
            var notional = amount * leverage;
            txtLeverageInfo.Text = $"Notioneel: {notional:N0} USDT";
        }

        // Risico als % van kapitaal + guardrail-vergelijking (max % per trade)
        var slNow = chkSL.IsChecked == true ? SafeValue(nbSL, 0) : 0;
        var riskPctNow = PositionSizeCalculator.RiskPctOfCapital(amount, entryPrice, slNow, _capital, leverage);
        if (riskPctNow > 0)
        {
            var limit = _settings.MaxPortfolioPercPerTrade;
            bool over = limit > 0 && riskPctNow > limit + 0.05;
            txtRiskInfo.Text = over
                ? $"⚠ {riskPctNow:0.0}% risico — boven je limiet van {limit:0.#}%"
                : $"{riskPctNow:0.0}% van kapitaal op het spel";
            txtRiskInfo.Foreground = over ? RedBrush : NeutralBrush;
        }
        else
        {
            txtRiskInfo.Text = string.Empty;
        }
    }

    private void UpdatePctLabel(TextBlock label, CheckBox chk, NumberBox nb, double entry, bool isLoss)
    {
        if (chk.IsChecked != true || entry <= 0) { label.Text = string.Empty; return; }
        var price = SafeValue(nb, 0);
        if (price <= 0) { label.Text = string.Empty; return; }

        var pct = (price - entry) / entry * 100.0;
        label.Text       = $"{pct:+0.00;-0.00}%";
        label.Foreground = pct < 0
            ? (isLoss ? NeutralBrush : RedBrush)
            : (isLoss ? RedBrush     : GreenBrush);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Action buttons
    // ────────────────────────────────────────────────────────────────────────

    private void OpenLong_Click(object sender, RoutedEventArgs e)
    {
        SelectedSide = OrderSide.Buy;
        Confirmed    = true;
        Hide();
    }

    private void OpenShort_Click(object sender, RoutedEventArgs e)
    {
        SelectedSide = OrderSide.Sell;
        Confirmed    = true;
        Hide();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Build result
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the completed OrderRequest, or null if not confirmed / invalid.</summary>
    public OrderRequest? BuildOrderRequest()
    {
        if (!Confirmed) return null;

        var exchange = cmbExchange.SelectedItem is ComboBoxItem exItem
            ? exItem.Tag?.ToString() switch
            {
                "Mexc"    => ExchangeKind.Mexc,
                "Binance" => ExchangeKind.Bybit,   // map unknown → Bybit for now
                "KuCoin"  => ExchangeKind.Bybit,
                _         => ExchangeKind.Bybit,
            }
            : ExchangeKind.Bybit;

        var marketType = rdFutures.IsChecked == true ? MarketType.Futures
                       : rdMargin.IsChecked  == true ? MarketType.Margin
                       :                               MarketType.Spot;

        var orderType  = rdMarket.IsChecked == true ? OrderType.Market : OrderType.Limit;
        var limitPrice = orderType == OrderType.Limit ? SafeValue(nbLimitPrice, _currentPrice) : 0;
        var amount     = SafeValue(nbAmount, 100);
        var slPrice    = chkSL.IsChecked  == true ? SafeValue(nbSL,  0) : 0;
        var tp1Price   = chkTP1.IsChecked == true ? SafeValue(nbTP1, 0) : 0;
        var tp2Price   = chkTP2.IsChecked == true ? SafeValue(nbTP2, 0) : 0;
        var leverage   = GetLeverage();

        if (amount <= 0) return null;

        return new OrderRequest(
            exchange, SelectedSide, marketType, orderType,
            amount, limitPrice, slPrice, tp1Price, tp2Price, leverage,
            Tp1ClosePct: tp1Price > 0 ? _tp1ClosePct : 100,
            Tp2ClosePct: tp2Price > 0 ? _tp2ClosePct : 100);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private double EffectiveEntryPrice()
        => rdLimit.IsChecked == true
            ? SafeValue(nbLimitPrice, _currentPrice)
            : _currentPrice;

    private int GetLeverage()
    {
        if (rdSpot.IsChecked == true) return 1;
        return cmbLeverage.SelectedItem is ComboBoxItem item
            && int.TryParse(item.Tag?.ToString(), out int lev) ? lev : 1;
    }

    private static double SafeValue(NumberBox nb, double fallback)
        => double.IsNaN(nb.Value) || nb.Value <= 0 ? fallback : nb.Value;

    private static string FormatPrice(double price) => price switch
    {
        >= 1000  => $"{price:#,0.00}",
        >= 1     => $"{price:F4}",
        >= 0.01  => $"{price:F6}",
        _        => $"{price:F8}",
    };

    private static string FormatQty(double qty) => qty switch
    {
        >= 1000  => $"{qty:#,0.00}",
        >= 1     => $"{qty:F4}",
        >= 0.001 => $"{qty:F6}",
        _        => $"{qty:F8}",
    };

    private static int GetDecimals(double price) => price switch
    {
        >= 1000  => 2,
        >= 1     => 4,
        >= 0.01  => 6,
        _        => 8,
    };

    /// <summary>
    /// Returns a NumberFormatter that displays the right number of decimal places
    /// for the given coin price, hiding IEEE-754 floating-point noise.
    /// </summary>
    private static Windows.Globalization.NumberFormatting.DecimalFormatter MakePriceFormatter(double price)
        => new()
        {
            FractionDigits = GetDecimals(price),
            IntegerDigits  = 1,
            IsGrouped      = false,
        };
}
