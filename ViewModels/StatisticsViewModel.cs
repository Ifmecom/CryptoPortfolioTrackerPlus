using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace CryptoPortfolioTracker.ViewModels;

public partial class StatisticsViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(StatisticsViewModel).PadRight(22));

    private readonly PortfolioService _portfolioService;

    // ── Period filter ────────────────────────────────────────────────────────
    [ObservableProperty] private string selectedPeriod = "Alles";
    public IReadOnlyList<string> PeriodOptions { get; } =
        new[] { "Alles", "Deze maand", "Afgelopen 3 maanden", "Dit jaar", "Aangepast" };

    // ── Custom date range (only active when SelectedPeriod == "Aangepast") ───
    [ObservableProperty] private bool isCustomPeriod;
    [ObservableProperty] private DateTimeOffset customStartDate = DateTimeOffset.Now.AddMonths(-1);
    [ObservableProperty] private DateTimeOffset customEndDate   = DateTimeOffset.Now;

    // ── Trade-kind filter ────────────────────────────────────────────────────
    [ObservableProperty] private string selectedTradeKind = "Alle";
    public IReadOnlyList<string> TradeKindOptions { get; } =
        new[] { "Alle", "Live", "Paper" };

    // ── Summary cards ────────────────────────────────────────────────────────
    [ObservableProperty] private string totalPnlDisplay   = "–";
    [ObservableProperty] private string winRateDisplay    = "–";
    [ObservableProperty] private string tradeCountDisplay = "–";
    [ObservableProperty] private string avgWinDisplay     = "–";
    [ObservableProperty] private string avgLossDisplay    = "–";
    [ObservableProperty] private string bestSymbolDisplay = "–";
    [ObservableProperty] private string worstSymbolDisplay= "–";
    [ObservableProperty] private string openTradesDisplay = "–";
    [ObservableProperty] private string totalVolumeDisplay= "–";

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.SolidColorBrush totalPnlBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));

    // ── Charts ───────────────────────────────────────────────────────────────
    [ObservableProperty] private IEnumerable<ISeries> winLossSeries  = Array.Empty<ISeries>();
    [ObservableProperty] private IEnumerable<ISeries> sideSeries     = Array.Empty<ISeries>();
    [ObservableProperty] private IEnumerable<ISeries> kindSeries     = Array.Empty<ISeries>();

    // ── Top performers table ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<SymbolStatRow> topSymbols = new();

    // ── Status ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = string.Empty;

    private bool _isDataLoaded;

    public StatisticsViewModel(PortfolioService portfolioService, Settings appSettings)
        : base(appSettings)
    {
        _portfolioService = portfolioService;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task ViewLoading()
    {
        if (!_isDataLoaded)
            await LoadAsync();
    }

    public void Terminate() { }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    partial void OnSelectedPeriodChanged(string value)
    {
        IsCustomPeriod = value == "Aangepast";
        _ = LoadAsync();
    }

    partial void OnSelectedTradeKindChanged(string value) =>
        _ = LoadAsync();

    partial void OnCustomStartDateChanged(DateTimeOffset value) =>
        _ = LoadAsync();

    partial void OnCustomEndDateChanged(DateTimeOffset value) =>
        _ = LoadAsync();

    // ── Data loading ─────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        IsLoading = true;
        StatusMessage = "Statistieken laden…";

        try
        {
            var query = context.ExchangeOrders.AsNoTracking().AsQueryable();

            if (SelectedPeriod == "Aangepast")
            {
                var start = CustomStartDate.UtcDateTime.Date;
                var end   = CustomEndDate.UtcDateTime.Date.AddDays(1); // einddag inclusief
                query = query.Where(o => o.CreatedAt >= start && o.CreatedAt < end);
            }
            else
            {
                var cutoff = GetCutoff();
                if (cutoff.HasValue)
                    query = query.Where(o => o.CreatedAt >= cutoff.Value);
            }

            if (SelectedTradeKind == "Live")
                query = query.Where(o => !o.IsPaper);
            else if (SelectedTradeKind == "Paper")
                query = query.Where(o => o.IsPaper);

            var orders = await query.ToListAsync();

            ComputeSummary(orders);
            BuildCharts(orders);
            BuildTopSymbols(orders);

            StatusMessage = $"{orders.Count} orders geladen — periode: {SelectedPeriod} · type: {SelectedTradeKind}";
            _isDataLoaded = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "StatisticsViewModel.LoadAsync failed");
            StatusMessage = $"Laden mislukt: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private DateTime? GetCutoff() => SelectedPeriod switch
    {
        "Deze maand"          => new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(),
        "Afgelopen 3 maanden" => DateTime.UtcNow.AddMonths(-3),
        "Dit jaar"            => new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(),
        _                     => (DateTime?)null,
    };

    // ── Summary computation ──────────────────────────────────────────────────

    private void ComputeSummary(List<ExchangeOrder> orders)
    {
        var closed = orders
            .Where(o => o.Status == OrderStatus.Closed && o.ClosePrice > 0 && o.Entry > 0)
            .ToList();

        var wins   = closed.Where(o => Pnl(o) > 0).ToList();
        var losses = closed.Where(o => Pnl(o) < 0).ToList();

        var totalPnl = closed.Sum(Pnl);

        TotalPnlDisplay = closed.Count == 0
            ? "–"
            : $"{totalPnl:+0.00;-0.00} USDT";

        TotalPnlBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            totalPnl > 0 ? Windows.UI.Color.FromArgb(0xFF, 0x3C, 0xB3, 0x71)
          : totalPnl < 0 ? Windows.UI.Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)
          : Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));

        WinRateDisplay = closed.Count == 0
            ? "–"
            : $"{100.0 * wins.Count / closed.Count:0.0} %";

        TradeCountDisplay = $"{wins.Count}W / {losses.Count}L / {closed.Count} totaal";

        AvgWinDisplay  = wins.Count   == 0 ? "–" : $"+{wins.Average(Pnl):0.00} USDT";
        AvgLossDisplay = losses.Count == 0 ? "–" : $"{losses.Average(Pnl):0.00} USDT";

        OpenTradesDisplay = orders.Count(o => o.Status == OrderStatus.Filled).ToString();
        TotalVolumeDisplay = closed.Count == 0
            ? "–"
            : $"{closed.Sum(o => o.Entry * o.Qty):N0} USDT";
    }

    private static double Pnl(ExchangeOrder o) =>
        o.Side == OrderSide.Buy
            ? Math.Round((o.ClosePrice - o.Entry) * o.Qty, 2)
            : Math.Round((o.Entry - o.ClosePrice) * o.Qty, 2);

    // ── Chart building ───────────────────────────────────────────────────────

    private void BuildCharts(List<ExchangeOrder> orders)
    {
        var closed = orders
            .Where(o => o.Status == OrderStatus.Closed && o.ClosePrice > 0 && o.Entry > 0)
            .ToList();

        int wins   = closed.Count(o => Pnl(o) > 0);
        int losses = closed.Count(o => Pnl(o) < 0);
        int even   = closed.Count(o => Pnl(o) == 0);

        // Win / Loss chart
        var winLoss = new List<ISeries>();
        if (wins + losses + even > 0)
        {
            if (wins > 0)   winLoss.Add(MakePie("Winst",    wins,   SKColor.Parse("#3CB371")));
            if (losses > 0) winLoss.Add(MakePie("Verlies",  losses, SKColor.Parse("#CD5C5C")));
            if (even > 0)   winLoss.Add(MakePie("Neutraal", even,   SKColor.Parse("#A0A0A0")));
        }
        WinLossSeries = winLoss;

        // Long / Short chart
        int longs  = closed.Count(o => o.Side == OrderSide.Buy);
        int shorts = closed.Count(o => o.Side == OrderSide.Sell);
        var side = new List<ISeries>();
        if (longs  > 0) side.Add(MakePie("Long",  longs,  SKColor.Parse("#5B9BD5")));
        if (shorts > 0) side.Add(MakePie("Short", shorts, SKColor.Parse("#ED7D31")));
        SideSeries = side;

        // Paper / Live chart  (use all orders, not just closed)
        int paper = orders.Count(o => o.IsPaper);
        int live  = orders.Count(o => !o.IsPaper);
        var kind = new List<ISeries>();
        if (paper > 0) kind.Add(MakePie("Paper", paper, SKColor.Parse("#9B59B6")));
        if (live  > 0) kind.Add(MakePie("Live",  live,  SKColor.Parse("#27AE60")));
        KindSeries = kind;
    }

    private static PieSeries<double> MakePie(string name, double value, SKColor color) =>
        new()
        {
            Name              = name,
            Values            = new[] { value },
            Fill              = new SolidColorPaint(color),
            DataLabelsPaint   = new SolidColorPaint(SKColors.White),
            DataLabelsSize    = 11,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
            DataLabelsFormatter = p => $"{name}: {value:0}",
            ToolTipLabelFormatter = p => $"{name}: {value:0} ({p.StackedValue!.Share:P0})",
        };

    // ── Top / bottom symbols ─────────────────────────────────────────────────

    private void BuildTopSymbols(List<ExchangeOrder> orders)
    {
        var closed = orders
            .Where(o => o.Status == OrderStatus.Closed && o.ClosePrice > 0 && o.Entry > 0)
            .ToList();

        var bySymbol = closed
            .GroupBy(o => o.Symbol)
            .Select(g =>
            {
                var cnt  = g.Count();
                var pnl  = g.Sum(Pnl);
                var rate = cnt == 0 ? 0.0 : 100.0 * g.Count(o => Pnl(o) > 0) / cnt;
                return new SymbolStatRow(g.Key, cnt, pnl, rate);
            })
            .OrderByDescending(r => r.TotalPnl)
            .ToList();

        // Best-/worst-display in summary cards
        BestSymbolDisplay  = bySymbol.FirstOrDefault()?.Symbol ?? "–";
        WorstSymbolDisplay = bySymbol.LastOrDefault()?.Symbol  ?? "–";

        // Table: top 5 best + top 5 worst (deduplicated)
        var top    = bySymbol.Take(5).ToList();
        var bottom = bySymbol.TakeLast(5).ToList();
        var table  = top.Union(bottom, SymbolStatRowComparer.Instance)
                        .OrderByDescending(r => r.TotalPnl)
                        .ToList();

        TopSymbols = new ObservableCollection<SymbolStatRow>(table);
    }
}

// ── Row helper ───────────────────────────────────────────────────────────────

public class SymbolStatRow
{
    public string Symbol   { get; }
    public int    Trades   { get; }
    public double TotalPnl { get; }
    public double WinRate  { get; }

    public string PnlDisplay     => $"{TotalPnl:+0.00;-0.00} USDT";
    public string WinRateDisplay => $"{WinRate:0.0} %";

    // Cached brush — set once when the row is constructed; safe for x:Bind
    public Microsoft.UI.Xaml.Media.SolidColorBrush PnlBrushCached { get; }

    public SymbolStatRow(string symbol, int trades, double totalPnl, double winRate)
    {
        Symbol   = symbol;
        Trades   = trades;
        TotalPnl = totalPnl;
        WinRate  = winRate;

        PnlBrushCached = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            TotalPnl > 0 ? Windows.UI.Color.FromArgb(0xFF, 0x3C, 0xB3, 0x71)
          : TotalPnl < 0 ? Windows.UI.Color.FromArgb(0xFF, 0xCD, 0x5C, 0x5C)
          :                Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));
    }
}

file sealed class SymbolStatRowComparer : IEqualityComparer<SymbolStatRow>
{
    public static readonly SymbolStatRowComparer Instance = new();
    public bool Equals(SymbolStatRow? x, SymbolStatRow? y) => x?.Symbol == y?.Symbol;
    public int GetHashCode(SymbolStatRow obj) => obj.Symbol.GetHashCode();
}
