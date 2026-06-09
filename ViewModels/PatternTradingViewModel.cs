using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace CryptoPortfolioTracker.ViewModels;

public partial class PatternTradingViewModel : BaseViewModel
{
    public static PatternTradingViewModel? Current;

    private readonly IPatternTradingService _patternService;
    private readonly IWatchlistService      _watchlistService;
    private readonly IWatchedSetupService   _watchedSetupService;

    // ── Observable state ────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<PatternCoinRow> displayItems = new();
    [ObservableProperty] private bool    isAnalyzing     = false;
    [ObservableProperty] private double  progressValue   = 0;
    [ObservableProperty] private string  statusText      = "Klik op 'Analyseer' om je portfolio te scannen.";
    [ObservableProperty] private string  lastRunText     = string.Empty;
    [ObservableProperty] private PatternFilter activeFilter = PatternFilter.All;

    // ── Sort / filter / search state ─────────────────────────────────────────
    [ObservableProperty] private string sortMode          = "Score";   // "Score" | "Change24h" | "Breakout"
    [ObservableProperty] private string listSearchText    = string.Empty;
    [ObservableProperty] private string tfFilter          = "All";     // "All" | "1D" | "4H" | "1H" | "15M"

    // ── Watchlist state ───────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<WatchlistItem> watchlistItems = new();
    [ObservableProperty] private bool watchlistExpanded = false;

    // ── Coin search state ─────────────────────────────────────────────────────
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool   isSearching = false;
    [ObservableProperty] private ObservableCollection<WatchlistSearchResult> searchResults = new();

    // Full result list — filtered copy goes to DisplayItems
    private List<PatternCoinAnalysis> _allResults = new();

    private CancellationTokenSource? _cts;

    // Debounce timer for live search
    private System.Timers.Timer? _searchDebounce;

    // Must be captured on the UI thread so we can marshal ObservableCollection updates back.
    private readonly DispatcherQueue? _dispatcherQueue;

    // ── Constructor ──────────────────────────────────────────────────────────

    private readonly IFundamentalsService _fundamentals;
    private IReadOnlyDictionary<string, CoinFundamentals> _fundMap =
        new Dictionary<string, CoinFundamentals>();

    public PatternTradingViewModel(
        IPatternTradingService patternService,
        IWatchlistService      watchlistService,
        IWatchedSetupService   watchedSetupService,
        IFundamentalsService   fundamentals,
        Settings               appSettings) : base(appSettings)
    {
        Current               = this;
        _patternService       = patternService;
        _watchlistService     = watchlistService;
        _watchedSetupService  = watchedSetupService;
        _fundamentals         = fundamentals;
        _dispatcherQueue  = DispatcherQueue.GetForCurrentThread();
        Logger = Log.Logger.ForContext(
            Constants.SourceContextPropertyName,
            typeof(PatternTradingViewModel).Name.PadRight(22));

        // Keep WatchlistCountText in sync when items are added/removed
        WatchlistItems.CollectionChanged += (_, _) =>
            OnPropertyChanged(nameof(WatchlistCountText));
    }

    public async Task ViewLoading()
    {
        await LoadWatchlistItemsAsync();
        try { _fundMap = await _fundamentals.GetScoreMapAsync(); }
        catch (Exception ex) { Logger.Warning(ex, "PatternTrading: fundamentals-map laden mislukt"); }
    }

    private async Task LoadWatchlistItemsAsync()
    {
        try
        {
            var items = await _watchlistService.GetAllAsync();
            _dispatcherQueue?.TryEnqueue(() =>
            {
                WatchlistItems.Clear();
                foreach (var item in items)
                    WatchlistItems.Add(item);
            });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: failed to load watchlist");
        }
    }

    public void Terminate()
    {
        _cts?.Cancel();
        _searchDebounce?.Dispose();
        Current = null;
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AnalyzePortfolio()
    {
        if (IsAnalyzing) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsAnalyzing   = true;
        ProgressValue = 0;
        StatusText    = "Marktdata ophalen…";
        DisplayItems.Clear();

        try
        {
            var progress = new Progress<int>(pct =>
            {
                ProgressValue = (double)pct;
                StatusText    = $"Analyseren… {pct}%";
            });

            _allResults = await _patternService.AnalyzePortfolioAsync(progress, _cts.Token);

            StatusText   = $"{_allResults.Count} coins geanalyseerd.";
            LastRunText  = $"Laatste analyse: {DateTime.Now:HH:mm}";

            // Auto-check watched setups against current prices from this analysis run
            var priceMap = _allResults
                .Where(r => r.Coin?.ApiId != null && r.Coin.Price > 0)
                .ToDictionary(r => r.Coin.ApiId!, r => r.Coin.Price);
            int updated = await _watchedSetupService.AutoUpdateStatusesAsync(priceMap);
            if (updated > 0)
                StatusText += $"  ·  {updated} setup(s) automatisch bijgewerkt.";

            ApplyFilter(ActiveFilter);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analyse gestopt.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PatternTrading: AnalyzePortfolio failed");
            StatusText = "Fout tijdens analyse — zie logbestand.";
        }
        finally
        {
            IsAnalyzing   = false;
            ProgressValue = 100;
        }
    }

    [RelayCommand]
    private void SetFilter(PatternFilter filter)
    {
        ActiveFilter = filter;
        ApplyFilter(filter);
    }

    [RelayCommand]
    private async Task CopyShareText(PatternCoinRow row)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(row.Analysis.ShareText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            StatusText = $"Setup van {row.Name} gekopieerd naar klembord ✓";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: clipboard copy failed");
            StatusText = "Kopiëren naar klembord mislukt.";
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddToWatchlist(WatchlistSearchResult result)
    {
        if (result is null) return;
        try
        {
            if (await _watchlistService.ExistsAsync(result.ApiId))
            {
                StatusText = $"{result.Name} staat al op de watchlijst.";
                return;
            }

            await _watchlistService.AddAsync(new WatchlistItem
            {
                ApiId    = result.ApiId,
                Name     = result.Name,
                Symbol   = result.Symbol,
                ImageUri = result.ImageUri,
                AddedAt  = DateTime.UtcNow,
            });

            StatusText  = $"✓ {result.Name} toegevoegd aan watchlijst.";
            SearchText  = string.Empty;
            SearchResults.Clear();
            await LoadWatchlistItemsAsync();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: AddToWatchlist failed for {Name}", result.Name);
            StatusText = $"Toevoegen mislukt voor {result.Name}.";
        }
    }

    [RelayCommand]
    private async Task RemoveFromWatchlist(PatternCoinRow row)
    {
        if (row is null) return;
        try
        {
            await _watchlistService.RemoveAsync(row.ApiId);
            _allResults.RemoveAll(r => r.Coin.ApiId == row.ApiId && r.IsWatchlist);
            ApplyFilter(ActiveFilter);
            await LoadWatchlistItemsAsync();
            StatusText = $"✓ {row.Name} verwijderd van watchlijst.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: RemoveFromWatchlist failed for {ApiId}", row.ApiId);
        }
    }

    /// <summary>Remove a coin directly from the watchlist panel (without row context).</summary>
    [RelayCommand]
    private async Task RemoveWatchlistItem(WatchlistItem item)
    {
        if (item is null) return;
        try
        {
            await _watchlistService.RemoveAsync(item.ApiId);
            _allResults.RemoveAll(r => r.Coin.ApiId == item.ApiId && r.IsWatchlist);
            ApplyFilter(ActiveFilter);
            _dispatcherQueue?.TryEnqueue(() => WatchlistItems.Remove(item));
            StatusText = $"✓ {item.Name} verwijderd van watchlijst.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: RemoveWatchlistItem failed for {ApiId}", item.ApiId);
        }
    }

    /// <summary>Lock this setup to the Setup Tracker tab for win-rate tracking.</summary>
    [RelayCommand]
    private async Task WatchSetup(PatternCoinRow row)
    {
        if (row?.Analysis is null) return;
        try
        {
            var a       = row.Analysis;
            var setup   = a.Setup;

            // Build a human-readable pattern summary (top 3 patterns by strength)
            var patternSummary = string.Join(" · ", a.Patterns
                .Where(p => p.Strength >= 55)
                .OrderByDescending(p => p.Strength)
                .Take(3)
                .Select(p => $"{p.DisplayName} {p.Timeframe}"));

            string direction = setup?.Direction ?? a.PrimaryDirection;
            double sl  = setup?.StopLoss ?? 0;
            double tp1 = setup?.Target1  ?? 0;

            // Guard: SL or TP1 missing → setup is incomplete and cannot be tracked
            if (sl <= 0 || tp1 <= 0)
            {
                StatusText = $"⚠ {row.Name}: SL of TP1 ontbreekt — setup kan niet worden gevolgd.";
                return;
            }

            // Guard: prevent duplicate (same coin + direction already Watching or Open)
            bool exists = await _watchedSetupService.ExistsAsync(
                a.Coin.ApiId ?? string.Empty, direction);
            if (exists)
            {
                StatusText = $"⚠ {row.Name} ({direction}) staat al in de Setup Tracker.";
                return;
            }

            // Capture BTC market regime as a proxy for overall market conditions at setup creation
            var btc = _allResults.FirstOrDefault(r =>
                string.Equals(r.Coin?.ApiId, "bitcoin", StringComparison.OrdinalIgnoreCase));
            string regime = btc?.Coin?.MarketRegime.ToString() ?? string.Empty;

            var watched = new WatchedSetup
            {
                CoinApiId             = a.Coin.ApiId ?? string.Empty,
                CoinName              = a.Coin.Name  ?? string.Empty,
                CoinSymbol            = a.Coin.Symbol?.ToUpperInvariant() ?? string.Empty,
                ImageUri              = a.Coin.ImageUri ?? string.Empty,
                Direction             = direction,
                EntryPrice            = setup?.EntryPrice  ?? a.Coin.Price,
                StopLoss              = sl,
                Target1               = tp1,
                Target2               = setup?.Target2     ?? 0,
                Score                 = a.TradabilityScore,
                PatternSummary        = patternSummary,
                Bias1D                = a.DailyBias,
                Bias4H                = a.H4Bias,
                MarketRegimeAtCreation = regime,
                AddedAt               = DateTime.UtcNow,
            };

            await _watchedSetupService.AddAsync(watched);
            StatusText = $"✓ {row.Name} toegevoegd aan Setup Tracker.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: WatchSetup failed for {Name}", row?.Name);
            StatusText = "Setup Tracker: opslaan mislukt.";
        }
    }

    /// <summary>Signal raised so code-behind can open the PaperTradeDialog for this row.</summary>
    public event Action<PatternCoinRow>? PaperTradeRequested;

    [RelayCommand]
    private void TakePaperTrade(PatternCoinRow row)
    {
        if (row is null) return;
        PaperTradeRequested?.Invoke(row);
    }

    [RelayCommand]
    private void SetSortMode(string mode)
    {
        SortMode = mode;
        ApplyFilter(ActiveFilter);
    }

    [RelayCommand]
    private void SetTfFilter(string tf)
    {
        TfFilter = tf;
        ApplyFilter(ActiveFilter);
    }

    // Called from the AutoSuggestBox TextChanged event (user typing — debounced).
    // Note: cannot be named OnSearchTextChanged — reserved by the [ObservableProperty] generator.
    public void HandleSearchInput(string text)
    {
        SearchText = text;

        _searchDebounce?.Dispose();
        if (string.IsNullOrWhiteSpace(text))
        {
            SearchResults.Clear();
            return;
        }

        // 400 ms debounce so we don't hit CoinGecko on every keystroke.
        // Elapsed fires on a ThreadPool thread — DoSearchAsync dispatches UI work back.
        _searchDebounce = new System.Timers.Timer(400) { AutoReset = false };
        _searchDebounce.Elapsed += async (_, _) =>
        {
            _searchDebounce?.Dispose();
            await DoSearchAsync(text);
        };
        _searchDebounce.Start();
    }

    // Called from the QuerySubmitted handler (magnifying-glass click or Enter key).
    // Bypasses the debounce and searches immediately.
    public async Task SearchImmediateAsync(string query)
    {
        _searchDebounce?.Dispose();
        SearchText = query;
        if (!string.IsNullOrWhiteSpace(query))
            await DoSearchAsync(query);
    }

    private async Task DoSearchAsync(string query)
    {
        // IsSearching may be set from any thread — dispatch to UI.
        _dispatcherQueue?.TryEnqueue(() => IsSearching = true);
        try
        {
            var results = await _watchlistService.SearchCoinsAsync(query);

            // ObservableCollection must only be modified on the UI thread.
            _dispatcherQueue?.TryEnqueue(() =>
            {
                SearchResults.Clear();
                foreach (var r in results)
                    SearchResults.Add(r);
                IsSearching = false;
            });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "PatternTrading: search failed for '{Query}'", query);
            _dispatcherQueue?.TryEnqueue(() =>
            {
                IsSearching = false;
                // Show rate-limit message to the user
                StatusText  = ex.Message.StartsWith("CoinGecko")
                    ? $"⚠ {ex.Message}"
                    : "Zoekopdracht mislukt — controleer uw internetverbinding.";
            });
        }
    }

    // ── Filtering ────────────────────────────────────────────────────────────

    private void ApplyFilter(PatternFilter filter)
    {
        // 1. Category filter
        var filtered = filter switch
        {
            PatternFilter.HighestScore  => _allResults.Where(r => r.TradabilityScore >= 60),
            PatternFilter.NearBreakout  => _allResults.Where(r => r.IsNearBreakout),
            PatternFilter.BullishOnly   => _allResults.Where(r => r.PrimaryDirection == "Long"),
            PatternFilter.BearishOnly   => _allResults.Where(r => r.PrimaryDirection == "Short"),
            _                           => _allResults.AsEnumerable(),
        };

        // 2. In-list text search (name or symbol)
        if (!string.IsNullOrWhiteSpace(ListSearchText))
        {
            var q = ListSearchText.Trim().ToUpperInvariant();
            filtered = filtered.Where(r =>
                (r.Coin.Name?.ToUpperInvariant().Contains(q) == true) ||
                (r.Coin.Symbol?.ToUpperInvariant().Contains(q) == true));
        }

        // 3. Timeframe filter — only include coins that have at least one pattern on TfFilter
        if (TfFilter != "All")
        {
            filtered = filtered.Where(r =>
                r.Patterns.Any(p => p.Timeframe == TfFilter));
        }

        // 4. Sort
        var sorted = SortMode switch
        {
            "Change24h" => filtered
                .OrderByDescending(r => r.Coin.Change24Hr)
                .ThenByDescending(r => r.TradabilityScore),
            "Breakout"  => filtered
                .Where(r => r.IsNearBreakout || r.ResistanceLevels.Any())
                .OrderBy(r => r.ResistanceLevels.Count > 0
                    ? r.ResistanceLevels.Min(lv => (lv.Price - r.Coin.Price) / r.Coin.Price)
                    : double.MaxValue)
                .ThenByDescending(r => r.TradabilityScore),
            _            => filtered   // default: by score
                .OrderByDescending(r => r.TradabilityScore)
                .ThenByDescending(r => r.IsNearBreakout),
        };

        var rows = sorted.Select(r => new PatternCoinRow(r)).ToList();

        // #1: fundamenteel kwaliteitsoordeel als badge naast de technische score
        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.ApiId) && _fundMap.TryGetValue(row.ApiId, out var f))
            {
                row.FundamentalScore   = f.TotalScore;
                row.FundamentalVerdict = f.Verdict;
                row.HasFundamental     = true;
            }
        }

        _dispatcherQueue?.TryEnqueue(() =>
        {
            DisplayItems.Clear();
            foreach (var row in rows)
                DisplayItems.Add(row);
        });
    }

    // Called when the in-list search text changes (from code-behind TextBox event).
    // Setting ListSearchText triggers OnListSearchTextChanged → ApplyFilter automatically.
    public void HandleListSearch(string text) => ListSearchText = text;

    partial void OnListSearchTextChanged(string value) => ApplyFilter(ActiveFilter);

    // ── Filter button states (for ToggleButton binding) ──────────────────────

    public bool FilterAll      => ActiveFilter == PatternFilter.All;
    public bool FilterHighest  => ActiveFilter == PatternFilter.HighestScore;
    public bool FilterBreakout => ActiveFilter == PatternFilter.NearBreakout;
    public bool FilterBullish  => ActiveFilter == PatternFilter.BullishOnly;
    public bool FilterBearish  => ActiveFilter == PatternFilter.BearishOnly;

    // ── TF filter button states ────────────────────────────────────────────────
    public bool TfAll  => TfFilter == "All";
    public bool Tf1D   => TfFilter == "1D";
    public bool Tf4H   => TfFilter == "4H";
    public bool Tf1H   => TfFilter == "1H";
    public bool Tf15M  => TfFilter == "15M";

    // ── Sort button states ─────────────────────────────────────────────────────
    public bool SortByScore    => SortMode == "Score";
    public bool SortByChange   => SortMode == "Change24h";
    public bool SortByBreakout => SortMode == "Breakout";

    partial void OnActiveFilterChanged(PatternFilter value)
    {
        OnPropertyChanged(nameof(FilterAll));
        OnPropertyChanged(nameof(FilterHighest));
        OnPropertyChanged(nameof(FilterBreakout));
        OnPropertyChanged(nameof(FilterBullish));
        OnPropertyChanged(nameof(FilterBearish));
    }

    partial void OnTfFilterChanged(string value)
    {
        OnPropertyChanged(nameof(TfAll));
        OnPropertyChanged(nameof(Tf1D));
        OnPropertyChanged(nameof(Tf4H));
        OnPropertyChanged(nameof(Tf1H));
        OnPropertyChanged(nameof(Tf15M));
    }

    partial void OnSortModeChanged(string value)
    {
        OnPropertyChanged(nameof(SortByScore));
        OnPropertyChanged(nameof(SortByChange));
        OnPropertyChanged(nameof(SortByBreakout));
    }

    // ── Watchlist count (for badge in the expander header) ────────────────────
    public string WatchlistCountText => WatchlistItems.Count > 0
        ? $"Watchlijst ({WatchlistItems.Count} coins)"
        : "Watchlijst";
}
