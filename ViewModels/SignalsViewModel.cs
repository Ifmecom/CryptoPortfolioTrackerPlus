using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Dialogs;
using CryptoPortfolioTracker.Infrastructure.Response.Coins;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CryptoPortfolioTracker.ViewModels;

public partial class SignalsViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(SignalsViewModel).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly IIndicatorService _indicatorService;
    private readonly ISignalEngine _signalEngine;
    private readonly ITradeService _tradeService;

    [ObservableProperty] private ObservableCollection<CoinSignalRow> rows = new();
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool isEvaluating;
    [ObservableProperty] private string portfolioName = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;

    // Skip DB+JSON reload on re-navigation; Refresh Analysis always forces a reload.
    private bool _isDataLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderRank), nameof(HeaderName), nameof(HeaderMacd),
        nameof(HeaderBollinger), nameof(HeaderAtr), nameof(HeaderStochRsi),
        nameof(HeaderSentiment), nameof(HeaderRegime), nameof(HeaderScore), nameof(HeaderDirection),
        nameof(HeaderEmaCross), nameof(HeaderRsi), nameof(HeaderMa50), nameof(HeaderAdx),
        nameof(HeaderPctB), nameof(HeaderSqueeze), nameof(HeaderHigh52w))]
    private string sortColumn = "Rank";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderRank), nameof(HeaderName), nameof(HeaderMacd),
        nameof(HeaderBollinger), nameof(HeaderAtr), nameof(HeaderStochRsi),
        nameof(HeaderSentiment), nameof(HeaderRegime), nameof(HeaderScore), nameof(HeaderDirection),
        nameof(HeaderEmaCross), nameof(HeaderRsi), nameof(HeaderMa50), nameof(HeaderAdx),
        nameof(HeaderPctB), nameof(HeaderSqueeze), nameof(HeaderHigh52w))]
    private bool sortAscending = true;

    // Column header texts with sort indicator
    public string HeaderRank      => "#"               + SortArrow("Rank");
    public string HeaderName      => "Asset"           + SortArrow("Name");
    public string HeaderMacd      => "MACD"            + SortArrow("Macd");
    public string HeaderBollinger => "Bollinger H / K / L" + SortArrow("BollingerUpper");
    public string HeaderAtr       => "ATR"             + SortArrow("Atr");
    public string HeaderStochRsi  => "StochRSI"        + SortArrow("StochRsi");
    public string HeaderSentiment => "Sentiment"       + SortArrow("Sentiment");
    public string HeaderRegime    => "Regime"          + SortArrow("Regime");
    public string HeaderScore     => "Score"           + SortArrow("CombinedScore");
    public string HeaderDirection => "Direction"       + SortArrow("Direction");
    public string HeaderEmaCross  => "EMA Cross"      + SortArrow("EmaCross");
    public string HeaderRsi       => "RSI 14"         + SortArrow("RsiDaily");
    public string HeaderMa50      => "MA50%"          + SortArrow("Ma50DistPerc");
    public string HeaderAdx       => "ADX"            + SortArrow("Adx");
    public string HeaderPctB      => "%B"             + SortArrow("BollingerPctB");
    public string HeaderSqueeze   => "Squeeze"        + SortArrow("IsSqueeze");
    public string HeaderHigh52w   => "52w%"           + SortArrow("High52wPerc");

    private string SortArrow(string col) =>
        SortColumn == col ? (SortAscending ? " ▲" : " ▼") : "";

    public SignalsViewModel(
        PortfolioService portfolioService,
        IIndicatorService indicatorService,
        ISignalEngine signalEngine,
        ITradeService tradeService,
        Settings appSettings)
        : base(appSettings)
    {
        _portfolioService = portfolioService;
        _indicatorService = indicatorService;
        _signalEngine     = signalEngine;
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

    /// <summary>Recalculate raw TA indicators only (MACD, Bollinger, ATR, StochRSI).</summary>
    [RelayCommand]
    private async Task RefreshAnalysis()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        StatusMessage = "Calculating indicators…";
        try
        {
            var context = _portfolioService.Context;
            if (context is null) return;

            var coins = await context.Coins.Where(c => c.IsAsset).ToListAsync();
            foreach (var coin in coins)
                await _indicatorService.RecalculateAllAsync(coin);

            await context.SaveChangesAsync();
            await LoadRowsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "RefreshAnalysis failed");
            StatusMessage = "Refresh failed — check log for details.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>Run the full SignalEngine: combine TA + sentiment + regime → Signal rows.</summary>
    [RelayCommand]
    private async Task EvaluateSignals()
    {
        if (IsEvaluating) return;
        IsEvaluating = true;
        StatusMessage = "Running signal engine…";
        try
        {
            var signals = await _signalEngine.EvaluateAsync();
            await LoadRowsAsync();
            StatusMessage = signals.Count == 0
                ? $"Signal evaluation complete — no assets found. ({DateTime.Now:HH:mm:ss})"
                : $"Signal evaluation complete — {signals.Count} assets evaluated. ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "EvaluateSignals failed");
            StatusMessage = "Signal evaluation failed — check log for details.";
        }
        finally
        {
            IsEvaluating = false;
        }
    }

    /// <summary>Open the paper-trade dialog for the given row and place an order if confirmed.</summary>
    [RelayCommand]
    private async Task PlacePaperTrade(CoinSignalRow row)
    {
        if (row is null) return;

        var context = _portfolioService.Context;
        if (context is null) return;

        // Load the coin entity (tracked, so we can read Price)
        var coin = await context.Coins.FindAsync(row.CoinId);
        if (coin is null) return;

        // Load the linked Signal (may be null for rows without a prior evaluation)
        Signal? signal = null;
        if (row.SignalId.HasValue)
            signal = await context.Signals.FindAsync(row.SignalId.Value);

        // Show dialog
        var dialog = new PaperTradeDialog(row, AppSettings);
        dialog.XamlRoot = MainPage.Current?.XamlRoot;
        var result = await App.ShowContentDialogAsync(dialog);

        if (result != ContentDialogResult.Primary) return;

        var req = dialog.BuildOrderRequest();
        if (req is null) return;

        try
        {
            // Use a dummy Signal if none exists yet (SignalId will be null)
            signal ??= new Signal
            {
                CoinId    = coin.Id,
                CreatedAt = DateTime.UtcNow,
                Reasoning = "Manual paper trade (no prior signal evaluation)",
            };

            await _tradeService.PlacePaperAsync(coin, signal, req);
            StatusMessage = $"Paper {req.Side} order placed for {row.Symbol} — {req.AmountUsdt:F0} USDT.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PlacePaperTrade failed for {Symbol}", row.Symbol);
            StatusMessage = $"Order failed: {ex.Message}";
        }
    }

    /// <summary>Sort the rows by the given column; toggles direction when clicked twice.</summary>
    [RelayCommand]
    private void SortByColumn(string column)
    {
        if (SortColumn == column)
            SortAscending = !SortAscending;
        else
        {
            SortColumn    = column;
            SortAscending = true;
        }
        ApplySortToRows();
    }

    private void ApplySortToRows()
    {
        if (Rows.Count == 0) return;

        IEnumerable<CoinSignalRow> sorted = SortAscending
            ? SortColumn switch
            {
                "Rank"          => Rows.OrderBy(r => r.Rank),
                "Name"          => Rows.OrderBy(r => r.Name),
                "Macd"          => Rows.OrderBy(r => r.Macd),
                "BollingerUpper"=> Rows.OrderBy(r => r.BollingerUpper),
                "Atr"           => Rows.OrderBy(r => r.Atr),
                "StochRsi"      => Rows.OrderBy(r => r.StochRsi),
                "Sentiment"     => Rows.OrderBy(r => r.Sentiment),
                "Regime"        => Rows.OrderBy(r => r.Regime),
                "CombinedScore" => Rows.OrderBy(r => r.CombinedScore),
                "Direction"     => Rows.OrderBy(r => r.Direction),
                "EmaCross"      => Rows.OrderBy(r => r.EmaCross),
                "RsiDaily"      => Rows.OrderBy(r => r.RsiDaily),
                "Ma50DistPerc"  => Rows.OrderBy(r => r.Ma50DistPerc),
                "Adx"           => Rows.OrderBy(r => r.Adx),
                "BollingerPctB" => Rows.OrderBy(r => r.BollingerPctB),
                "IsSqueeze"     => Rows.OrderBy(r => r.IsSqueeze ? 1.0 : 0.0),
                "High52wPerc"   => Rows.OrderBy(r => r.High52wPerc),
                _               => Rows.OrderBy(r => r.Rank),
            }
            : SortColumn switch
            {
                "Rank"          => Rows.OrderByDescending(r => r.Rank),
                "Name"          => Rows.OrderByDescending(r => r.Name),
                "Macd"          => Rows.OrderByDescending(r => r.Macd),
                "BollingerUpper"=> Rows.OrderByDescending(r => r.BollingerUpper),
                "Atr"           => Rows.OrderByDescending(r => r.Atr),
                "StochRsi"      => Rows.OrderByDescending(r => r.StochRsi),
                "Sentiment"     => Rows.OrderByDescending(r => r.Sentiment),
                "Regime"        => Rows.OrderByDescending(r => r.Regime),
                "CombinedScore" => Rows.OrderByDescending(r => r.CombinedScore),
                "Direction"     => Rows.OrderByDescending(r => r.Direction),
                "EmaCross"      => Rows.OrderByDescending(r => r.EmaCross),
                "RsiDaily"      => Rows.OrderByDescending(r => r.RsiDaily),
                "Ma50DistPerc"  => Rows.OrderByDescending(r => r.Ma50DistPerc),
                "Adx"           => Rows.OrderByDescending(r => r.Adx),
                "BollingerPctB" => Rows.OrderByDescending(r => r.BollingerPctB),
                "IsSqueeze"     => Rows.OrderByDescending(r => r.IsSqueeze ? 1.0 : 0.0),
                "High52wPerc"   => Rows.OrderByDescending(r => r.High52wPerc),
                _               => Rows.OrderByDescending(r => r.Rank),
            };

        Rows = new ObservableCollection<CoinSignalRow>(sorted);
    }

    // -----------------------------------------------------------------------
    // Data loading
    // -----------------------------------------------------------------------

    private async Task LoadRowsAsync()
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        var coins = await context.Coins
            .AsNoTracking()
            .Where(c => c.IsAsset)
            .OrderBy(c => c.Rank)
            .ToListAsync();

        // Fetch latest signal per coin (load all, group in memory — simple & reliable with SQLite)
        var coinIds = coins.Select(c => c.Id).ToList();
        var allSignals = await context.Signals
            .AsNoTracking()
            .Where(s => coinIds.Contains(s.CoinId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var signalMap = allSignals
            .GroupBy(s => s.CoinId)
            .ToDictionary(g => g.Key, g => g.First());

        // Build rows with sparkline trend data
        var rowList = new List<CoinSignalRow>(coins.Count);
        foreach (var coin in coins)
        {
            var (t1h, t4h, tDay) = await LoadTrendDataAsync(coin);
            rowList.Add(new CoinSignalRow(coin, signalMap.GetValueOrDefault(coin.Id),
                                          t1h, t4h, tDay));
        }

        Rows = new ObservableCollection<CoinSignalRow>(rowList);

        // Re-apply current sort (default is by Rank when SortColumn=="Rank")
        ApplySortToRows();

        StatusMessage = Rows.Count == 0
            ? "No assets found."
            : Rows.All(r => !r.HasSignal)
                ? "No signal data yet — press Evaluate Signals to run the engine."
                : $"Showing analysis for {Rows.Count} assets.";
        _isDataLoaded = true;
    }

    /// <summary>
    /// Load daily closes from the MarketChart JSON file and slice into three windows:
    ///   Trend1h  → last 14 daily closes (short-term, 2-week proxy for 1 H chart)
    ///   Trend4h  → last 30 daily closes (medium-term, monthly proxy for 4 H chart)
    ///   TrendDay → last 90 daily closes (long-term, quarterly daily chart)
    /// </summary>
    private static async Task<(IReadOnlyList<double> t1h, IReadOnlyList<double> t4h, IReadOnlyList<double> tDay)>
        LoadTrendDataAsync(Coin coin)
    {
        try
        {
            var suffix = coin.Name.Contains("_pre-listing") ? "-prelisting" : "";
            var fileName = Path.Combine(AppConstants.ChartsFolder, $"MarketChart_{coin.ApiId}{suffix}.json");
            if (!File.Exists(fileName))
                return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

            var chart = new MarketChartById();
            var result = await chart.LoadMarketChartJson(coin.ApiId + suffix);

            if (chart.Prices is null || chart.Prices.Length == 0)
                return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

            // Extract closing prices (index [1] in each pair)
            var allCloses = chart.Prices
                .Where(p => p.Length >= 2 && p[1].HasValue)
                .Select(p => (double)p[1]!.Value)
                .ToArray();

            if (allCloses.Length == 0)
                return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

            IReadOnlyList<double> Slice(int n) =>
                allCloses.Length >= n
                    ? allCloses[^n..]        // last n elements
                    : allCloses;             // use all available if fewer

            return (Slice(14), Slice(30), Slice(90));
        }
        catch
        {
            return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        }
    }
}
