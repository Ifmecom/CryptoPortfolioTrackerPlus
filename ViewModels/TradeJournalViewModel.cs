using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Core;
using System.Collections.ObjectModel;

namespace CryptoPortfolioTracker.ViewModels;

public partial class TradeJournalViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(TradeJournalViewModel).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly ITradeService    _tradeService;

    [ObservableProperty] private ObservableCollection<TradeJournalRow> rows = new();
    [ObservableProperty] private string portfolioName       = string.Empty;
    [ObservableProperty] private string statusMessage       = string.Empty;
    [ObservableProperty] private string filterLabel         = "Open";
    [ObservableProperty] private string totalPnlDisplay     = "–";
    [ObservableProperty] private string lastRefreshedDisplay = "–";
    [ObservableProperty] private Microsoft.UI.Xaml.Media.SolidColorBrush totalPnlBrush
        = new(Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));

    // Filter state
    private string _activeFilter = "Open"; // All | Open | Closed | Paper | Live

    // Active-filter indicators — bound by view to highlight the current tab
    public bool IsFilterAll    => _activeFilter == "All";
    public bool IsFilterOpen   => _activeFilter == "Open";
    public bool IsFilterClosed => _activeFilter == "Closed";
    public bool IsFilterPaper  => _activeFilter == "Paper";
    public bool IsFilterLive   => _activeFilter == "Live";

    // Last known price map — reused by Kill All without a separate DB round-trip
    private Dictionary<string, double> _lastPriceMap = new();

    // Skip full reload on re-navigation; manual Vernieuwen still forces a reload.
    private bool _isDataLoaded;

    /// <summary>Exposed so views can forward settings to dialogs they construct.</summary>
    public Settings Settings => AppSettings;

    public TradeJournalViewModel(PortfolioService portfolioService, ITradeService tradeService, Settings appSettings)
        : base(appSettings)
    {
        _portfolioService = portfolioService;
        _tradeService     = tradeService;
    }

    public async Task ViewLoading()
    {
        PortfolioName = _portfolioService.CurrentPortfolio?.Name ?? string.Empty;
        if (!_isDataLoaded)
            await LoadRowsAsync();
    }

    public void Terminate() { }

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task Refresh() => await LoadRowsAsync();

    [RelayCommand]
    private async Task SetFilter(string filter)
    {
        _activeFilter = filter;
        FilterLabel   = filter;
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterOpen));
        OnPropertyChanged(nameof(IsFilterClosed));
        OnPropertyChanged(nameof(IsFilterPaper));
        OnPropertyChanged(nameof(IsFilterLive));
        await LoadRowsAsync();
    }

    [RelayCommand]
    private async Task CloseOrder(TradeJournalRow row)
    {
        if (row is null || !row.IsCloseable) return;

        var currentPrice = row.CurrentPrice;
        double pnl = row.Order.Side == OrderSide.Buy
            ? Math.Round((currentPrice - row.Entry) * row.Qty, 2)
            : Math.Round((row.Entry - currentPrice) * row.Qty, 2);

        var dialog = new ContentDialog
        {
            Title             = $"Positie sluiten — {row.Symbol}",
            Content           = $"Sluit {row.Side} positie op de huidige marktprijs van {currentPrice:#,0.########} USDT?\n\n" +
                                $"Geschatte P&L: {pnl:+0.00;-0.00} USDT",
            PrimaryButtonText = "Ja, sluiten",
            CloseButtonText   = "Annuleren",
            XamlRoot          = MainPage.Current?.XamlRoot,
            RequestedTheme    = AppSettings.AppTheme,
        };

        var result = await App.ShowContentDialogAsync(dialog);
        if (result != ContentDialogResult.Primary) return;

        try
        {
            await _tradeService.ClosePaperAsync(row.Order, currentPrice);
            await LoadRowsAsync();
            StatusMessage = $"Positie {row.Symbol} gesloten @ {currentPrice:#,0.########} — P&L: {pnl:+0.00;-0.00} USDT";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "CloseOrder failed for order #{Id}", row.Id);
            StatusMessage = $"Sluiten mislukt: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task KillAll()
    {
        var openCount = Rows.Count(r => r.IsCloseable);
        if (openCount == 0)
        {
            StatusMessage = "Geen open papierposities om te sluiten.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title             = "⚠️  Kill All — weet je het zeker?",
            Content           = $"Dit sluit ALLE {openCount} open papierposities direct op de huidige marktprijs.\n\n" +
                                 "Dit kan niet ongedaan gemaakt worden.",
            PrimaryButtonText = "Ja, alles sluiten",
            CloseButtonText   = "Annuleren",
            XamlRoot          = MainPage.Current?.XamlRoot,
            RequestedTheme    = AppSettings.AppTheme,
        };

        var result = await App.ShowContentDialogAsync(dialog);
        if (result != ContentDialogResult.Primary) return;

        try
        {
            var closed = await _tradeService.CloseAllPaperAsync(_lastPriceMap);
            await LoadRowsAsync();
            StatusMessage = $"Kill All: {closed} posities gesloten.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "KillAll failed");
            StatusMessage = $"Kill All mislukt: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CancelOrder(TradeJournalRow row)
    {
        if (row is null) return;

        var dialog = new ContentDialog
        {
            Title             = "Cancel Order",
            Content           = $"Cancel paper order for {row.Symbol}?",
            PrimaryButtonText = "Yes, cancel",
            CloseButtonText   = "No",
            XamlRoot          = MainPage.Current?.XamlRoot,
            RequestedTheme    = AppSettings.AppTheme,
        };

        var result = await App.ShowContentDialogAsync(dialog);
        if (result != ContentDialogResult.Primary) return;

        var context = _portfolioService.Context;
        if (context is null) return;

        var order = await context.ExchangeOrders.FindAsync(row.Id);
        if (order is null) return;

        try
        {
            await _tradeService.CancelAsync(order);
            await LoadRowsAsync();
            StatusMessage = $"Order #{row.Id} cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "CancelOrder failed for order #{Id}", row.Id);
            StatusMessage = $"Cancel failed: {ex.Message}";
        }
    }

    // -----------------------------------------------------------------------
    // Notes editing (called from code-behind after dialog confirmed)
    // -----------------------------------------------------------------------

    public async Task SaveNoteAsync(int orderId, string notes)
    {
        try
        {
            await _tradeService.UpdateNotesAsync(orderId, notes);
            await LoadRowsAsync();
            StatusMessage = "Notitie opgeslagen.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SaveNote failed for order #{Id}", orderId);
            StatusMessage = $"Opslaan mislukt: {ex.Message}";
        }
    }

    // -----------------------------------------------------------------------
    // Trade level editing (called from code-behind after dialog confirmed)
    // -----------------------------------------------------------------------

    public async Task SaveTradeEditsAsync(TradeJournalRow row, double sl, double tp1, double tp2)
    {
        try
        {
            await _tradeService.UpdateOrderLevelsAsync(row.Id, sl, tp1, tp2);
            await LoadRowsAsync();
            StatusMessage = $"Trade {row.Symbol} bijgewerkt — SL/TP aangepast.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "SaveTradeEdits failed for order #{Id}", row.Id);
            StatusMessage = $"Aanpassen mislukt: {ex.Message}";
        }
    }

    // -----------------------------------------------------------------------
    // Data loading
    // -----------------------------------------------------------------------

    private async Task LoadRowsAsync()
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        var query = context.ExchangeOrders.AsNoTracking().AsQueryable();

        query = _activeFilter switch
        {
            "Open"   => query.Where(o => o.Status == OrderStatus.Filled || o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyFilled),
            "Closed" => query.Where(o => o.Status == OrderStatus.Closed || o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.Rejected),
            "Paper"  => query.Where(o => o.IsPaper),
            "Live"   => query.Where(o => !o.IsPaper),
            _        => query,
        };

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(500)
            .ToListAsync();

        // Load current coin prices for PnL estimate
        var coinSymbols = orders
            .Select(o => o.Symbol.Replace("USDT", "").ToLowerInvariant())
            .Distinct()
            .ToList();

        var coins = await context.Coins
            .AsNoTracking()
            .Where(c => coinSymbols.Contains(c.Symbol.ToLower()))
            .ToListAsync();

        var priceMap = coins.ToDictionary(
            c => c.Symbol.ToUpperInvariant(),
            c => c.Price,
            StringComparer.OrdinalIgnoreCase);

        _lastPriceMap = priceMap;   // keep for Kill All

        // ── Auto-close any orders whose TP/SL has been hit ──────────────────
        var triggered = await _tradeService.AutoCloseTriggeredAsync(priceMap);
        if (triggered.Count > 0)
        {
            // Reload orders so the closed ones appear with correct status
            orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Take(500)
                .ToListAsync();

            var autoMsg = string.Join(", ", triggered.Select(t => $"{t.Symbol} {t.Reason}"));
            StatusMessage = $"⚡ Auto-gesloten: {autoMsg}";
        }

        Rows = new ObservableCollection<TradeJournalRow>(
            orders.Select(o => new TradeJournalRow(o, priceMap)));

        // Total PnL across all rows that have a value
        var totalPnl = Rows.Sum(r => r.PnlUsdt);
        TotalPnlDisplay = totalPnl == 0
            ? "–"
            : $"{totalPnl:+0.00;-0.00} USDT";
        TotalPnlBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            totalPnl > 0 ? Windows.UI.Color.FromArgb(0xFF, 0x3C, 0xB3, 0x71)  // green
          : totalPnl < 0 ? Windows.UI.Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)  // red
          : Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));               // grey

        StatusMessage        = Rows.Count == 0
            ? "No trades found."
            : $"{Rows.Count} trade(s) — filter: {_activeFilter}";
        LastRefreshedDisplay = $"Prijzen: {DateTime.Now:HH:mm:ss}";
        _isDataLoaded = true;
    }
}

// -----------------------------------------------------------------------
// Row projection
// -----------------------------------------------------------------------

public class TradeJournalRow
{
    public int       Id           { get; }
    public string    Symbol       { get; }
    public string    Side         { get; }
    public double    Entry        { get; }
    public double    StopLoss     { get; }
    public double    TakeProfit   { get; }
    public double    Qty          { get; }
    public string    Status       { get; }
    public string    Kind         { get; }   // Paper / Live
    public string    Exchange     { get; }
    public DateTime  CreatedAt    { get; }
    public DateTime? FilledAt     { get; }
    public double    CurrentPrice { get; }
    public double    ClosePrice   { get; }   // != 0 when user manually closed the position
    public double    PnlUsdt      { get; }
    public double    PnlPerc      { get; }   // % gain/loss vs entry
    public double    RMultiple    { get; }   // PnL / 1R (entry - stopLoss) * qty

    // Backing field used by close/kill-all commands in the view
    public ExchangeOrder Order { get; }

    public string Notes { get; set; } = string.Empty;

    // ── Display helpers ──────────────────────────────────────────────────

    public string EntryDisplay        => Entry        == 0 ? "–" : $"{Entry:#,0.########}";
    public string CurrentPriceDisplay => CurrentPrice == 0 ? "–" : $"{CurrentPrice:#,0.########}";
    public string SlDisplay           => StopLoss     == 0 ? "–" : $"{StopLoss:#,0.########}";
    public string TpDisplay         => TakeProfit == 0 ? "–" : $"{TakeProfit:#,0.########}";
    public string ClosePriceDisplay => ClosePrice == 0 ? "–" : $"{ClosePrice:#,0.########}";
    public string QtyDisplay        => Qty        == 0 ? "–" : $"{Qty:#,0.########}";
    public string CreatedDisplay    => CreatedAt.ToString("dd-MM-yy HH:mm");

    public string PnlDisplay => PnlUsdt == 0 ? "–"
        : $"{PnlUsdt:+0.00;-0.00} USDT\n{PnlPerc:+0.0;-0.0} %";

    /// <summary>Live ongerealiseerde winst/verlies in USDT-waarde — alleen voor open (Filled) posities.</summary>
    public string UnrealisedPnlDisplay =>
        Status == "Filled" && PnlUsdt != 0 ? $"{PnlUsdt:+0.00;-0.00} USDT" : "–";

    public string RDisplay => RMultiple == 0 ? "–" : $"{RMultiple:+0.0;-0.0}R";

    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "—" : Notes;

    public bool IsCancellable => Status is "Pending" or "PartiallyFilled";

    /// <summary>True for open paper positions that the user can close at current price.</summary>
    public bool IsCloseable => Status == "Filled" && Order.IsPaper && CurrentPrice > 0;

    /// <summary>True for open or pending paper trades whose SL/TP can be edited.</summary>
    public bool IsEditable => Status is "Filled" or "Pending" && Order.IsPaper;

    // PnL colour: green / red / grey — stored once in constructor, never reallocated per render.
    public Microsoft.UI.Xaml.Media.SolidColorBrush PnlBrush { get; }

    // ── Constructor ──────────────────────────────────────────────────────

    public TradeJournalRow(ExchangeOrder order, Dictionary<string, double> priceMap)
    {
        Order      = order;
        Id         = order.Id;
        Symbol     = order.Symbol;
        Side       = order.Side.ToString();
        Entry      = order.Entry;
        StopLoss   = order.StopLoss;
        TakeProfit = order.TakeProfit;
        Qty        = order.Qty;
        Status     = order.Status.ToString();
        Kind       = order.IsPaper ? "Paper" : "Live";
        Exchange   = order.Exchange.ToString();
        CreatedAt  = order.CreatedAt.ToLocalTime();
        FilledAt   = order.FilledAt?.ToLocalTime();
        ClosePrice = order.ClosePrice;
        Notes      = order.Notes ?? string.Empty;

        var baseSymbol = order.Symbol.Replace("USDT", "").ToUpperInvariant();
        CurrentPrice = priceMap.GetValueOrDefault(baseSymbol);

        // For manually closed positions: use recorded ClosePrice for realised P&L
        // For open (Filled) positions:  use live CurrentPrice for unrealised P&L
        double pnlPrice = order.Status == OrderStatus.Closed && order.ClosePrice > 0
            ? order.ClosePrice
            : CurrentPrice;

        if ((order.Status == OrderStatus.Filled || order.Status == OrderStatus.Closed)
            && pnlPrice > 0 && Entry > 0)
        {
            PnlUsdt = order.Side == OrderSide.Buy
                ? Math.Round((pnlPrice - Entry) * Qty, 2)
                : Math.Round((Entry - pnlPrice) * Qty, 2);

            PnlPerc = order.Side == OrderSide.Buy
                ? Math.Round((pnlPrice - Entry) / Entry * 100.0, 2)
                : Math.Round((Entry - pnlPrice) / Entry * 100.0, 2);

            // R-multiple: how many R's of risk earned/lost
            var riskPerUnit = Math.Abs(Entry - StopLoss);
            if (riskPerUnit > 0)
                RMultiple = Math.Round(PnlUsdt / (riskPerUnit * Qty), 2);
        }

        PnlBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            PnlUsdt > 0 ? Windows.UI.Color.FromArgb(0xFF, 0x3C, 0xB3, 0x71)
          : PnlUsdt < 0 ? Windows.UI.Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)
          : Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));
    }
}
