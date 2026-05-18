using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using CryptoPortfolioTracker.Infrastructure;
using CryptoPortfolioTracker.Infrastructure.Response.Coins;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.ViewModels;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Pipes;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Polly;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class PriceUpdateService : IPriceUpdateService
{
    private readonly PeriodicTimer timer;
    private readonly CancellationTokenSource cts = new();
    private readonly IIndicatorService _indicatorService;
    private readonly Settings _appSettings;
    private readonly PortfolioService _portfolioService;
    private readonly IPriceLevelService _priceLevelService;
    private readonly IAssetService _assetService;
    private readonly IMessenger _messenger;

    private UpdateContext currentContext;
    private readonly CoinGeckoApiClient _geckoClient;
    private static ILogger Logger { get; set; } = Log.Logger.ForContext(Constants.SourceContextPropertyName, typeof(PriceUpdateService).Name.PadRight(22));
    public bool IsPausRequested { get; private set; }
    public bool IsUpdating { get; private set; }
    //private bool isInit;

    private Task? timerTask;
    private Task? resumeTask;



    public PriceUpdateService(PortfolioService portfolioService, 
                                IAssetService assetService, 
                                IPriceLevelService priceLevelService, 
                                IMessenger messenger, 
                                Settings appSettings,
                                IIndicatorService indicatorService)
    {
        _indicatorService = indicatorService;
        _appSettings = appSettings;
        _portfolioService = portfolioService;
        currentContext = _portfolioService.UpdateContext;

        _priceLevelService = priceLevelService;
        _assetService = assetService;
        _messenger = messenger;

        IsPausRequested = false;
        timer = new(System.TimeSpan.FromMinutes(_appSettings.PriceUpdateIntervalMinutes));
        _geckoClient = new CoinGeckoApiClient(AppConstants.ApiPath, AppConstants.CoinGeckoApiKey);
    }

    public void Start()
    {
        Logger.Information("PriceUpdateService started");
        currentContext = _portfolioService.UpdateContext;
        IsUpdating = false;
        timerTask = DoWorkAsync();
    }

    private async Task DoWorkAsync()
    {
        try
        {
           // isInit = true;
            await UpdatePricesAllCoins();
           // isInit = false;
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                if (!IsPausRequested)
                {
                    if (resumeTask == null || resumeTask.IsCompleted)
                    {
                        resumeTask = null;
                        Logger.Information("NextTick received");
                        await UpdatePricesAllCoins();
                    }
                    else
                    {
                        Logger.Information("NextTick received => service waiting for resume");
                    }
                }
                else
                {
                    Logger.Information("NextTick received => service paused");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("PriceUpdateService cancelled");
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "PriceUpdateService stopped unexpected");
        }
    }

    public void Stop()
    {
        if (timerTask is null)
        {
            return;
        }
        cts.Cancel();
        cts.Dispose();
        Logger.Information("PriceUpdateService stopped");
    }

    public void Pause(bool isDisconnecting = false)
    {
        IsPausRequested = true;
        Logger.Information("PriceUpdateService Paused");
    }

    public void Resume()
    {
        IsPausRequested = false;
        if (currentContext != _portfolioService.UpdateContext)
        {
            if (timerTask is null)
            {
                Logger.Information("PriceUpdateService re-started at Resume");
                currentContext = _portfolioService.UpdateContext;
                IsUpdating = false;
                timerTask = DoWorkAsync();
            }
            else
            {
                resumeTask = Task.Run(async () =>
                {
                    Logger.Information("PriceUpdateService continued with new context");
                    currentContext = _portfolioService.UpdateContext;
                    await UpdatePricesAllCoins();
                });
            }
        }
        else
        {
            Logger.Information("PriceUpdateService continued with existing context");
        }
    }

    private async Task UpdatePricesAllCoins()
    {
        IsUpdating = true;
        await Task.Delay(100);
        await App.UpdateSemaphore.WaitAsync();
        bool isReleased = false;
        
        try
        {
            var context = _portfolioService.UpdateContext;
            var coinIdsTemp = await context.Coins
                //.AsNoTracking()
                .Where(x => x.Name.Length <= 12 || (x.Name.Length > 12 && x.Name.Substring(x.Name.Length - 12) != "_pre-listing"))
                .Include(x => x.PriceLevels)
                .Select(c => c.ApiId)
                .ToListAsync();

            isReleased = App.UpdateSemaphore.Release() == 0;

            if (!coinIdsTemp.Any())
            {
                return;
            }
            var coinIds = ShiftCoinIdsRandom(coinIdsTemp);

            var dataPerPage = 100;
            var nrOfPages = (int)Math.Ceiling((double)coinIds.Count / dataPerPage);
            var result = new Result<bool>();

            var coinIdsPerPage = SplitCoinIdsPerPageAndJoin(coinIds, dataPerPage, nrOfPages);

            for (var pageNr = 1; pageNr <= nrOfPages; pageNr++)
            {
                if (cts.IsCancellationRequested || IsPausRequested)
                {
                    return;
                }
                var coinMarketsResult = await GetMarketDataFromGecko(coinIdsPerPage[pageNr - 1], dataPerPage);
                // Await the result of IfSucc to ensure it completes before proceeding
                await coinMarketsResult.Match(
                    async list =>
                    {
                        result = await UpdatePricesWithMarketData(list);
                    },
                    err =>
                    {
                        result = new Result<bool>(err);
                        return Task.CompletedTask;
                    }
                    );

                if (nrOfPages > 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }

                MainPage.Current.DispatcherQueue.TryEnqueue(() =>
                {
                    _assetService.SortList();
                    _ = _assetService.CalculateAssetsTotalValues().ContinueWith(
                        t => Logger.Error(t.Exception!.GetBaseException(), "CalculateAssetsTotalValues failed"),
                        System.Threading.CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);
                });
            }
            Logger.Information($"All coins are updated.");
            _priceLevelService.UpdateHeatMap();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, $"UpdatePricesAllCoins failed.");
        }
        finally
        {
            if (!isReleased)
            {
                App.UpdateSemaphore.Release();
            };
            IsUpdating = false;
        }
    }

    private static List<string> ShiftCoinIdsRandom(List<string> coinIds)
    {
        var startIndex = new Random().Next(0, coinIds.Count - 1);
        var shiftedCoinIds = new List<string>();

        for (var i = startIndex; i < coinIds.Count + startIndex; i++)
        {
            var j = i >= coinIds.Count ? i - coinIds.Count : i;
            shiftedCoinIds.Add(coinIds[j]);
        }
        return shiftedCoinIds;
    }

    private static string[] SplitCoinIdsPerPageAndJoin(List<string> coinIds, int dataPerPage, int nrOfPages)
    {
        var coinIdsPerPage = new string[nrOfPages];
        var dataToGo = coinIds.Count;

        for (var pageNr = 1; pageNr <= nrOfPages; pageNr++)
        {
            var dataToTake = dataToGo <= dataPerPage ? dataToGo : dataPerPage;
            coinIdsPerPage[pageNr - 1] = string.Join(",", coinIds.Skip((pageNr - 1) * dataPerPage).Take(dataToTake));
            dataToGo -= dataToTake;
        }
        return coinIdsPerPage;
    }

    private async Task<Result<List<CoinMarkets>>> GetMarketDataFromGecko(string coinIds, int dataPerPage)
    {
        var retries = 0;
        var totalRequests = 0;

        var strategy = new ResiliencePipelineBuilder().AddRetry(new()
        {
            ShouldHandle = new PredicateBuilder().Handle<System.Exception>(),
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(30),
            OnRetry = args =>
            {
                Logger.Debug(args.Outcome.Exception!, "Getting Market Data; OnRetry ({0})", retries);
                retries++;
                return default;
            }
        }).Build();

        List<CoinMarkets>? coinMarketsPage = null;

        var tokenSource = new CancellationTokenSource();
        var cancellationToken = tokenSource.Token;
        while (!cancellationToken.IsCancellationRequested && !IsPausRequested)
        {
            totalRequests++;
            try
            {
                await strategy.ExecuteAsync(async token =>
                {
                    Logger.Debug("Getting Market Data; (Retries: {0})", retries);
                    coinMarketsPage = await _geckoClient.GetCoinMarketsAsync(coinIds, dataPerPage, token);
                }, cancellationToken);

                Logger.Information("Received Market Data; (Count: {0})", coinMarketsPage?.Count.ToString() ?? "0");
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "Getting Market Data failed after {0} requests", totalRequests);
                return new Result<List<CoinMarkets>>(ex);
            }
            finally
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }
        }
        return coinMarketsPage ?? new Result<List<CoinMarkets>>(new NullReferenceException());
    }

    private async Task<Result<bool>> UpdatePricesWithMarketData(List<CoinMarkets> marketDataList)
    {
        if (marketDataList == null) return new Result<bool>(new ArgumentNullException(nameof(marketDataList)));

        Logger.Information($"Updating Market Data. {marketDataList.Count}");

        // ── Pre-load all coins in ONE batch query (replaces N individual SingleAsync calls) ──
        await App.UpdateSemaphore.WaitAsync();
        Dictionary<string, Coin> coinMap;
        try
        {
            var context = _portfolioService.UpdateContext;
            context.ChangeTracker?.Clear();

            var apiIds = marketDataList
                .Select(m => m.Id.ToLower())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var coins = await context.Coins
                .AsNoTracking()
                .Include(x => x.PriceLevels)
                .Where(c => apiIds.Contains(c.ApiId.ToLower()))
                .ToListAsync();

            coinMap = coins.ToDictionary(c => c.ApiId.ToLower(), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            App.UpdateSemaphore.Release();
        }

        foreach (var coinData in marketDataList)
        {
            if (!coinMap.TryGetValue(coinData.Id.ToLower(), out var coin)) continue;

            var coinResult = await UpdatePriceCoin(coinData, coin);
            if (coinResult.IsFaulted)
            {
                return new Result<bool>(false);
            }
        }
        MainPage.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _messenger.Send(new UpdatePricesMessage());
        });
        return true;
    }

    /// <summary>
    /// Updates a single coin price in the UpdateContext.
    /// The <paramref name="coin"/> must be a detached (AsNoTracking) entity pre-loaded
    /// by <see cref="UpdatePricesWithMarketData"/> — no per-coin DB fetch needed.
    /// </summary>
    private async Task<Result<Coin>> UpdatePriceCoin(CoinMarkets coinData, Coin coin)
    {
        await App.UpdateSemaphore.WaitAsync();
        var context = _portfolioService.UpdateContext;

        try
        {
            var oldPrice = coin.Price;
            var newPrice = coinData.CurrentPrice ?? 0;

            if (oldPrice != newPrice)
            {
                coin.Price = newPrice;
                coin.MarketCap = coinData.MarketCap ?? 0;
                coin.ImageUri = coinData.Image.AbsoluteUri?.Replace("large", "small") ?? string.Empty;
                coin.Rank = coinData.MarketCapRank ?? 999999;
                coin.Change24Hr = coinData.PriceChangePercentage24HInCurrency ?? 0;
                coin.Ath = coinData.Ath ?? 0;
                coin.Change1Month = coinData.PriceChangePercentage30DInCurrency ?? 0;
                coin.Change52Week = coinData.PriceChangePercentage1YInCurrency ?? 0;

                // Coin was loaded AsNoTracking — re-attach for the update
                context.Coins.Update(coin);
                Logger.Information("Updating {0} {1} => {2}", coin.Name, oldPrice, newPrice);

                await context.SaveChangesAsync();

                // Detach so the next coin iteration starts with a clean tracker
                context.Entry(coin).State = EntityState.Detached;
                foreach (var pl in coin.PriceLevels)
                    context.Entry(pl).State = EntityState.Detached;

                // Reflect the changes in the PortfolioContext (UI context)
                var entity = await _portfolioService.Context.Coins.FindAsync(coin.Id);
                if (entity != null)
                    await _portfolioService.Context.Entry(entity).ReloadAsync();

                // Run indicator calculations
                await _indicatorService.CalculateRsiAsync(coin);
                await _indicatorService.CalculateMaAsync(coin);
                _indicatorService.EvaluatePriceLevels(coin, newPrice);
                coin.NotifyDerivedValuesChanged();
            }
            return coin;
        }
        catch (JsonException jsonEx)
        {
            Logger.Error(jsonEx, "JSON processing error for coin: {0}", coinData.Name);
            return new Result<Coin>(jsonEx);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Updating Prices {coinData.Name} failed.");
            return new Result<Coin>(ex);
        }
        finally
        {
            App.UpdateSemaphore.Release();
        }
    }


}


