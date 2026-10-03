using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoPortfolioTracker.Dialogs;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services;
using CryptoPortfolioTracker.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Core;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace CryptoPortfolioTracker.ViewModels;

public partial class TradeAnalysisViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(TradeAnalysisViewModel).PadRight(22));

    private readonly PortfolioService      _portfolioService;
    private readonly ITradeAnalysisService _analysisService;
    private readonly ITradeService         _tradeService;
    private readonly IFundamentalsService  _fundamentals;

    [ObservableProperty] private ObservableCollection<Coin> coins = new();
    [ObservableProperty] private Coin? selectedCoin;
    // IsLoading is inherited from BaseViewModel
    [ObservableProperty] private bool isAnalyzingAll;
    [ObservableProperty] private string statusMessage    = string.Empty;
    [ObservableProperty] private string portfolioName    = string.Empty;
    [ObservableProperty] private TradeAnalysisResult?               currentAnalysis;
    [ObservableProperty] private IReadOnlyList<CoinAnalysisSummary>? allResults;
    [ObservableProperty] private DateTime? allResultsGeneratedAt;

    // Richtingsfilter voor de "Analyseer alles"-lijst: "All" | "Long" | "Short" | "None".
    [ObservableProperty] private string overviewDir = "All";

    // #1: fundamenteel kwaliteitsoordeel van de geanalyseerde coin
    [ObservableProperty] private string fundamentalDisplay = string.Empty;
    [ObservableProperty] private bool   hasFundamental;

    public TradeAnalysisViewModel(
        PortfolioService portfolioService,
        ITradeAnalysisService analysisService,
        ITradeService tradeService,
        IFundamentalsService fundamentals,
        Settings appSettings,
        ITradingViewService? tradingView = null)
        : base(appSettings)
    {
        _portfolioService = portfolioService;
        _analysisService  = analysisService;
        _tradeService     = tradeService;
        _fundamentals     = fundamentals;
        _tradingView      = tradingView;

        _initializingTop = true;
        TopPickCount = appSettings.GetTopPickCount(TopPage);
        OnlyTopPicks = appSettings.GetTopPicksOnly(TopPage);
        _initializingTop = false;
    }

    // -----------------------------------------------------------------------
    // Top X (v1.48) — de view tekent opnieuw bij een wijziging (PropertyChanged)
    // -----------------------------------------------------------------------

    private const string TopPage = "TradeAnalysis";
    private bool _initializingTop;

    [ObservableProperty] private int    topPickCount;
    [ObservableProperty] private bool   onlyTopPicks;
    [ObservableProperty] private string topPickSummary = string.Empty;

    partial void OnTopPickCountChanged(int value)
    {
        if (!_initializingTop) AppSettings.SetTopPickCount(TopPage, value);
    }

    partial void OnOnlyTopPicksChanged(bool value)
    {
        if (!_initializingTop) AppSettings.SetTopPicksOnly(TopPage, value);
    }

    /// <summary>
    /// Rangschikt de (op richting gefilterde) overzichtsrijen en geeft terug wat getoond moet worden:
    /// alles in de bestaande volgorde (top gemarkeerd) of — bij "alleen top" — de top X op rang.
    /// Trade Advies kijkt naar trend en momentum: kwaliteit = score in de eigen richting (Short = 100 − score), R/R naar TP1, en een
    /// waarschuwing als de richting tegen de daily-trend ingaat. Geen gemeten trefkans voor deze bron.
    /// </summary>
    public List<CoinAnalysisSummary> PrepareOverview(IEnumerable<CoinAnalysisSummary> filtered)
    {
        var list = filtered.ToList();
        OpportunityRanker.Apply(list, s => new OpportunityInput(
            Key:          s.Coin.ApiId ?? s.Coin.Symbol ?? s.Coin.Id.ToString(),
            Direction:    s.Direction,
            // Score is hier "hoe bullish" (≥ 60 Long, ≤ 40 Short — zie BuildTradeSetup): sterkte in eigen richting.
            Quality:      s.Direction == "Short" ? 100 - s.Score : s.Score,
            RiskReward:   s.RiskReward1 > 0 ? s.RiskReward1 : null,
            CounterTrend: TrendAlignment.IsCounterTrend(s.Direction, s.DailyBias),
            Eligible:     s.SetupValid && s.EntryPrice > 0,
            // Volatiliteit is al door de setup-poort gecontroleerd (atr: null); hier marktwaarde/rang/instorting.
            Health:       CoinHealth.Evaluate(s.Coin.Price, s.Coin.MarketCap, s.Coin.Rank, s.Coin.Change1Month,
                              atr: null, ma50DistPct: s.Coin.Ma50DistPerc == 0 ? null : s.Coin.Ma50DistPerc,
                              minAtrFraction: 0)), TopPickCount);

        int candidates = list.Count(s => s.TopRank > 0);
        TopPickSummary = TopPickCount == 0 || candidates == 0
            ? (candidates == 0 && list.Count > 0 ? "geen beoordeelbare setups in deze lijst" : string.Empty)
            : $"top {Math.Min(TopPickCount, candidates)} van {candidates} setups";

        return OpportunityRanker.Visible(list, TopPickCount, OnlyTopPicks);
    }

    // -----------------------------------------------------------------------
    // TradingView (v1.48) — grafiek, Pine Script en watchlist; de view toont de vensters
    // -----------------------------------------------------------------------

    private readonly ITradingViewService? _tradingView;

    private string TickerFor(string symbol, string dataSource)
        => _tradingView?.TickerFor(symbol, dataSource) ?? TradingViewSymbol.For(symbol, dataSource);

    /// <summary>Ticker van de huidige analyse, of null als er niets geanalyseerd is.</summary>
    public string? CurrentTicker => CurrentAnalysis is { } r ? TickerFor(r.Symbol, r.DataSource) : null;

    /// <summary>Pine-setup voor de huidige analyse (met steun/weerstand), of null zonder geldige setup.</summary>
    public PineSetup? CurrentPineSetup()
    {
        if (CurrentAnalysis is not { } r || r.Setup is not { IsValid: true } s) return null;
        var setup = new PineSetup(
            Ticker:      TickerFor(r.Symbol, r.DataSource),
            Name:        r.CoinName,
            Direction:   s.Direction,
            Entry:       s.EntryPrice,
            StopLoss:    s.StopLoss,
            Target1:     s.Target1,
            Target2:     s.Target2,
            Score:       r.CombinedScore,
            Source:      "Trade Advies",
            Note:        $"Vertrouwen {s.Confidence} · R/R {s.RiskReward1:0.0}",
            Supports:    r.SupportLevels,
            Resistances: r.ResistanceLevels);
        return setup.IsValid ? setup : null;
    }

    private static PineSetup ToPine(CoinAnalysisSummary s, string ticker) => new(
        Ticker:    ticker,
        Name:      s.Coin.Name ?? s.Coin.Symbol ?? ticker,
        Direction: s.Direction,
        Entry:     s.EntryPrice,
        StopLoss:  s.StopLoss,
        Target1:   s.Target1,
        Score:     s.Score,
        Source:    "Trade Advies",
        Note:      s.TopRank > 0 ? $"Top #{s.TopRank} · kansscore {s.KansScore:0}" : string.Empty);

    /// <summary>De top-setups uit 'Analyseer alles' (op rang); zonder Top X alle geldige setups.</summary>
    public List<PineSetup> TopPineSetups()
    {
        var all = AllResults ?? Array.Empty<CoinAnalysisSummary>();
        var rows = all.Any(s => s.IsTopPick)
            ? all.Where(s => s.IsTopPick).OrderBy(s => s.TopRank)
            : all.Where(s => s.SetupValid && s.EntryPrice > 0).OrderByDescending(s => s.KansScore);
        return rows.Select(s => ToPine(s, TickerFor(s.Coin.Symbol ?? string.Empty, s.DataSource)))
                   .Where(p => p.IsValid).ToList();
    }

    /// <summary>Watchlist: eerst de top-setups, daarna alle geanalyseerde munten.</summary>
    public (string Content, int Count) BuildTradingViewWatchlist()
    {
        var all  = AllResults ?? Array.Empty<CoinAnalysisSummary>();
        var top  = TopPineSetups().Select(p => p.Ticker).ToList();
        var rest = all.Select(s => TickerFor(s.Coin.Symbol ?? string.Empty, s.DataSource))
                      .Where(t => !string.IsNullOrEmpty(t)).ToList();
        if (rest.Count == 0 && CurrentTicker is { } cur) rest.Add(cur);
        var content = TradingViewWatchlist.Build(new (string, IEnumerable<string>)[]
        {
            ("CPT Top-setups", top),
            ("CPT Trade Advies", rest),
        });
        return (content, top.Concat(rest).Distinct().Count());
    }

    [RelayCommand]
    private async Task OpenTradingView()
    {
        if (_tradingView is null || CurrentTicker is not { } ticker) return;
        StatusMessage = await _tradingView.OpenChartAsync(ticker)
            ? $"TradingView geopend ({ticker})."
            : "Openen van TradingView is mislukt.";
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        try
        {
            PortfolioName = _portfolioService.CurrentPortfolio?.Name ?? string.Empty;

            var ctx = _portfolioService.Context;
            var allCoins = await ctx.Coins
                .Where(c => c.IsAsset)
                .OrderBy(c => c.Rank)
                .ToListAsync();

            Coins.Clear();
            foreach (var c in allCoins)
                Coins.Add(c);

            if (Coins.Any())
                SelectedCoin = Coins.First();

            StatusMessage = $"{Coins.Count} coins — selecteer een coin en klik Analyseer, of klik Analyseer alles.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize TradeAnalysisViewModel");
            StatusMessage = "Fout bij laden van coins.";
        }
    }

    // -----------------------------------------------------------------------
    // Single-coin analysis
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (SelectedCoin is null)
        {
            StatusMessage = "Selecteer eerst een coin.";
            return;
        }

        IsLoading       = true;
        CurrentAnalysis = null;
        StatusMessage   = $"Data ophalen voor {SelectedCoin.Name}...";

        try
        {
            var result = await _analysisService.GenerateAsync(SelectedCoin);
            CurrentAnalysis = result;
            StatusMessage   = $"Analyse gereed — {result.GeneratedAt:HH:mm:ss} — bron: {result.DataSource}";

            // #1: fundamenteel kwaliteitsoordeel erbij tonen (indien geanalyseerd in de Fundamentals-tab)
            try
            {
                var f = await _fundamentals.GetAsync(SelectedCoin.ApiId);
                HasFundamental     = f is not null;
                FundamentalDisplay = f is not null ? $"Ⓕ {f.TotalScore:0} · {f.Verdict}" : string.Empty;
            }
            catch { HasFundamental = false; FundamentalDisplay = string.Empty; }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "TradeAnalysis failed for {Coin}", SelectedCoin.Name);
            StatusMessage = "Analyse mislukt. Controleer je internetverbinding.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // -----------------------------------------------------------------------
    // Analyseer alles
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task AnalyzeAllAsync()
    {
        if (!Coins.Any()) return;

        IsAnalyzingAll  = true;
        CurrentAnalysis = null;
        AllResults      = null;
        StatusMessage   = $"Alle {Coins.Count} coins analyseren...";

        var bag       = new ConcurrentBag<CoinAnalysisSummary>();
        var semaphore = new SemaphoreSlim(3, 3);   // max 3 parallel exchange-fetches
        int completed = 0;
        int total     = Coins.Count;

        var tasks = Coins.Select(async coin =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result  = await _analysisService.GenerateAsync(coin);
                var summary = new CoinAnalysisSummary(coin, result);
                bag.Add(summary);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AnalyzeAll: failed for {Coin}", coin.Name);
            }
            finally
            {
                semaphore.Release();
                int done = Interlocked.Increment(ref completed);
                StatusMessage = $"Analyseren… {done}/{total}";
            }
        }).ToList();

        await Task.WhenAll(tasks);

        // Sort: Long (score desc) → Short (score asc = sterkste short eerst) → Geen signaal
        var sorted = bag
            .OrderBy(s => s.Direction == "Long"  ? 0 :
                          s.Direction == "Short" ? 1 : 2)
            .ThenBy(s => s.Direction == "Long"  ? -s.Score :
                         s.Direction == "Short" ?  s.Score : 0)
            .ToList();

        int signals = sorted.Count(s => s.Direction is "Long" or "Short");
        AllResultsGeneratedAt = DateTime.Now;
        AllResults    = sorted;
        IsAnalyzingAll = false;
        StatusMessage  = $"{signals} trade-signalen gevonden in {total} coins — {DateTime.Now:HH:mm:ss}";
    }

    // -----------------------------------------------------------------------
    // Paper trade vanuit single-coin analyse
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task PlacePaperTradeAsync()
    {
        if (SelectedCoin is null || CurrentAnalysis is null) return;

        var setup = CurrentAnalysis.Setup;
        if (setup.Direction == "Geen signaal")
        {
            StatusMessage = "Geen signaal — er is geen trade setup om uit te voeren.";
            return;
        }

        var dialog = new PaperTradeDialog(SelectedCoin, setup, AppSettings);
        dialog.XamlRoot = MainPage.Current?.XamlRoot;
        await App.ShowContentDialogAsync(dialog);
        if (!dialog.Confirmed) return;

        var req = dialog.BuildOrderRequest();
        if (req is null) return;

        try
        {
            var dummySignal = new Signal
            {
                CoinId    = SelectedCoin.Id,
                CreatedAt = DateTime.UtcNow,
                Direction = setup.Direction == "Short" ? Enums.SignalDirection.Short : Enums.SignalDirection.Long,
                Reasoning = setup.Reasoning.Any()
                    ? string.Join("; ", setup.Reasoning)
                    : $"Trade Advies — {setup.Direction} ({setup.Confidence})",
            };

            await _tradeService.PlacePaperAsync(SelectedCoin, dummySignal, req);
            StatusMessage = $"Paper {req.Side} order geplaatst voor {SelectedCoin.Symbol?.ToUpperInvariant()} — {req.AmountUsdt:F0} USDT.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PlacePaperTrade (TradeAdvies) failed for {Coin}", SelectedCoin.Name);
            StatusMessage = $"Order mislukt: {ex.Message}";
        }
    }

    // -----------------------------------------------------------------------
    // Jump to single-coin analysis from the ranked list
    // -----------------------------------------------------------------------

    public async Task AnalyzeCoinFromSummaryAsync(CoinAnalysisSummary summary)
    {
        SelectedCoin = summary.Coin;
        await AnalyzeAsync();
    }
}
