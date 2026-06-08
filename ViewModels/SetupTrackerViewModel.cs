using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using LanguageExt.Common;
using Microsoft.UI.Dispatching;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.ViewModels;

public partial class SetupTrackerViewModel : BaseViewModel
{
    public static SetupTrackerViewModel? Current;

    private readonly IWatchedSetupService _service;
    private readonly ILibraryService      _libraryService;
    private readonly IMessenger           _messenger;
    private readonly DispatcherQueue?     _dispatcherQueue;

    // ── Observable state ─────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<WatchedSetup> setups    = new();
    [ObservableProperty] private string statusFilter  = "All";   // "All" | "Watching" | "Won" | "Lost" | "Expired"
    [ObservableProperty] private string statusText    = string.Empty;

    // ── Stats ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private int    totalCount    = 0;
    [ObservableProperty] private int    wonCount      = 0;
    [ObservableProperty] private int    lostCount     = 0;
    [ObservableProperty] private int    watchingCount = 0;
    [ObservableProperty] private int    openCount     = 0;
    [ObservableProperty] private double winRatePct    = 0;

    // ── Price freshness ───────────────────────────────────────────────────────
    [ObservableProperty] private string pricesTimestamp = string.Empty;

    // Computed display helpers
    public string WinRateText      => $"{WinRatePct:F0}%";
    public string WinRateColor     => WinRatePct >= 50 ? "#4caf50" : "#ef5350";
    public string WinRateTarget    => WinRatePct >= 50 ? "✓ Boven 50% doel" : $"⚠ Doel: 50% ({50 - (int)WinRatePct}% nog nodig)";

    // ── Constructor ───────────────────────────────────────────────────────────

    public SetupTrackerViewModel(
        IWatchedSetupService service,
        ILibraryService      libraryService,
        IMessenger           messenger,
        Settings             appSettings)
        : base(appSettings)
    {
        Current          = this;
        _service         = service;
        _libraryService  = libraryService;
        _messenger       = messenger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Logger = Log.Logger.ForContext(
            Constants.SourceContextPropertyName,
            nameof(SetupTrackerViewModel).PadRight(22));

        // Auto-refresh whenever PriceUpdateService finishes a price cycle
        messenger.Register<UpdatePricesMessage>(this, async (r, m) =>
        {
            await RefreshAsync();
        });
    }

    public async Task ViewLoadingAsync()
    {
        await RefreshAsync();
    }

    public void Terminate()
    {
        _messenger.UnregisterAll(this);
        Current = null;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task Refresh() => await RefreshAsync();

    [RelayCommand]
    private void SetFilter(string filter)
    {
        StatusFilter = filter;
        OnPropertyChanged(nameof(FilterAll));
        OnPropertyChanged(nameof(FilterWatching));
        OnPropertyChanged(nameof(FilterOpen));
        OnPropertyChanged(nameof(FilterWon));
        OnPropertyChanged(nameof(FilterLost));
        OnPropertyChanged(nameof(FilterExpired));
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task Expire(WatchedSetup setup)
    {
        if (setup is null) return;
        try
        {
            await _service.ExpireAsync(setup.Id);
            await RefreshAsync();
            StatusText = $"'{setup.CoinName}' setup verlopen verklaard.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SetupTracker: Expire failed for {Id}", setup.Id);
        }
    }

    [RelayCommand]
    private async Task Remove(WatchedSetup setup)
    {
        if (setup is null) return;
        try
        {
            await _service.RemoveAsync(setup.Id);
            await RefreshAsync();
            StatusText = $"'{setup.CoinName}' setup verwijderd.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SetupTracker: Remove failed for {Id}", setup.Id);
        }
    }

    [RelayCommand]
    private async Task CloseAsWon(WatchedSetup setup)
    {
        if (setup is null) return;
        try
        {
            double closePrice = setup.CurrentPrice > 0 ? setup.CurrentPrice : setup.Target1;
            await _service.CloseManuallyAsync(setup.Id, WatchedSetupStatus.Won, closePrice);
            await RefreshAsync();
            StatusText = $"✅ '{setup.CoinName}' handmatig als Gewonnen gemarkeerd.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SetupTracker: CloseAsWon failed for {Id}", setup.Id);
        }
    }

    [RelayCommand]
    private async Task CloseAsLost(WatchedSetup setup)
    {
        if (setup is null) return;
        try
        {
            double closePrice = setup.CurrentPrice > 0 ? setup.CurrentPrice : setup.StopLoss;
            await _service.CloseManuallyAsync(setup.Id, WatchedSetupStatus.Lost, closePrice);
            await RefreshAsync();
            StatusText = $"❌ '{setup.CoinName}' handmatig als Verloren gemarkeerd.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SetupTracker: CloseAsLost failed for {Id}", setup.Id);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        try
        {
            // Build price map — start from DB (always available) so prices show
            // even when the Library/Assets page hasn't been visited yet this session.
            var priceByApiId = await BuildPriceMapAsync();

            // Auto-detect entry triggers and TP/SL hits against current prices
            if (priceByApiId.Count > 0)
                await _service.AutoUpdateStatusesAsync(priceByApiId);

            var all   = await _service.GetAllAsync();
            var stats = await _service.GetStatsAsync();

            // Populate live CurrentPrice on every setup (not stored in DB)
            foreach (var setup in all)
            {
                if (priceByApiId.TryGetValue(setup.CoinApiId, out double price))
                    setup.CurrentPrice = price;
            }

            var filtered = StatusFilter switch
            {
                "Watching" => all.Where(s => s.Status == WatchedSetupStatus.Watching),
                "Open"     => all.Where(s => s.Status == WatchedSetupStatus.Open),
                "Won"      => all.Where(s => s.Status == WatchedSetupStatus.Won),
                "Lost"     => all.Where(s => s.Status == WatchedSetupStatus.Lost),
                "Expired"  => all.Where(s => s.Status == WatchedSetupStatus.Expired),
                _          => all.AsEnumerable(),
            };

            var list = filtered.ToList();

            string timestamp = priceByApiId.Count > 0
                ? $"Prijzen: {DateTime.Now:HH:mm:ss}"
                : string.Empty;

            _dispatcherQueue?.TryEnqueue(() =>
            {
                Setups.Clear();
                foreach (var s in list)
                    Setups.Add(s);

                TotalCount       = stats.Total;
                WonCount         = stats.Won;
                LostCount        = stats.Lost;
                WatchingCount    = stats.Watching;
                OpenCount        = stats.Open;
                WinRatePct       = stats.WinRatePct;
                PricesTimestamp  = timestamp;

                OnPropertyChanged(nameof(WinRateText));
                OnPropertyChanged(nameof(WinRateColor));
                OnPropertyChanged(nameof(WinRateTarget));
            });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "SetupTracker: RefreshAsync failed");
        }
    }

    /// <summary>
    /// Builds a CoinApiId → Price lookup.
    /// Queries the DB via GetCoinsFromContext so prices are available even when
    /// the Library page hasn't been visited yet (ListCoins would be empty).
    /// Live in-memory prices from ListCoins override DB values where present.
    /// </summary>
    private async Task<Dictionary<string, double>> BuildPriceMapAsync()
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // 1. Load from DB — always available, covers all portfolio coins
        var dbResult = await _libraryService.GetCoinsFromContext();
        dbResult.IfSucc(coins =>
        {
            foreach (var c in coins)
            {
                if (c.ApiId != null && c.Price > 0)
                    map[c.ApiId] = c.Price;
            }
        });

        // 2. Override with live in-memory prices where available (more up-to-date)
        foreach (var c in _libraryService.ListCoins)
        {
            if (c.ApiId != null && c.Price > 0)
                map[c.ApiId] = c.Price;
        }

        return map;
    }

    // ── Filter button states ──────────────────────────────────────────────────
    public bool FilterAll      => StatusFilter == "All";
    public bool FilterWatching => StatusFilter == "Watching";
    public bool FilterOpen     => StatusFilter == "Open";
    public bool FilterWon      => StatusFilter == "Won";
    public bool FilterLost     => StatusFilter == "Lost";
    public bool FilterExpired  => StatusFilter == "Expired";
}
