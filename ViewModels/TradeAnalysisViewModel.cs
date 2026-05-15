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

    [ObservableProperty] private ObservableCollection<Coin> coins = new();
    [ObservableProperty] private Coin? selectedCoin;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isAnalyzingAll;
    [ObservableProperty] private string statusMessage    = string.Empty;
    [ObservableProperty] private string portfolioName    = string.Empty;
    [ObservableProperty] private TradeAnalysisResult?               currentAnalysis;
    [ObservableProperty] private IReadOnlyList<CoinAnalysisSummary>? allResults;
    [ObservableProperty] private DateTime? allResultsGeneratedAt;

    public TradeAnalysisViewModel(
        PortfolioService portfolioService,
        ITradeAnalysisService analysisService,
        ITradeService tradeService,
        Settings appSettings)
        : base(appSettings)
    {
        _portfolioService = portfolioService;
        _analysisService  = analysisService;
        _tradeService     = tradeService;
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
