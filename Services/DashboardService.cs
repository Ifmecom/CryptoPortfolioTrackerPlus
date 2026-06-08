
using CommunityToolkit.Mvvm.ComponentModel;
using CryptoPortfolioTracker.Models;
using Serilog;
using System.Collections.Generic;
using CryptoPortfolioTracker.Enums;
using Serilog.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using LanguageExt;
using CryptoPortfolioTracker.Helpers;
using LanguageExt.Common;
using System.Diagnostics;

namespace CryptoPortfolioTracker.Services;


public partial class DashboardService : ObservableObject, IDashboardService
{
    private static ILogger Logger { get; set; } = Log.Logger.ForContext(Constants.SourceContextPropertyName, typeof(DashboardService).Name.PadRight(22));

    private readonly IIndicatorService _indicatorService;
    private readonly Settings _appSettings;
    private readonly PortfolioService _portfolioService;
    private readonly IAssetService _assetService;
    private readonly INarrativeService _narrativeService;
    private readonly IAccountService _accountService;
    private readonly IFearGreedService _fearGreedService;

    public DashboardService(PortfolioService portfolioService,
                        IAssetService assetService,
                        INarrativeService narrativeService,
                        IAccountService accountService,
                        Settings appSettings,
                        IIndicatorService indicatorService,
                        IFearGreedService fearGreedService)
    {
        _indicatorService = indicatorService;
        _appSettings = appSettings;
        _portfolioService = portfolioService;
        _assetService = assetService;
        _accountService = accountService;
        _narrativeService = narrativeService;
        _fearGreedService = fearGreedService;
    }

    public PortfolioContext GetContext()
    {
        return _portfolioService.Context;
    }

    public async Task CalculateIndicatorsAllCoins()
    {
        // AsNoTracking: read-only, geen save nodig. ToListAsync: niet blokkeren op UI-thread.
        var coins = await _portfolioService.Context.Coins.AsNoTracking().ToListAsync();

        foreach (var coin in coins)
        {
            await _indicatorService.CalculateRsiAsync(coin);
            await _indicatorService.CalculateMaAsync(coin);
            // Task.Delay(10) verwijderd — indicator-berekeningen lezen lokale JSON-bestanden,
            // geen netwerk-rateLimiting nodig. 50 coins × 10ms = 500ms onnodige latentie.
        }
    }
    public async Task CalculateRsiAllCoins()
    {
        var coins = await _portfolioService.Context.Coins.AsNoTracking().ToListAsync();

        foreach (var coin in coins)
        {
            await _indicatorService.CalculateRsiAsync(coin);
        }
    }
    public async Task CalculateMaAllCoins()
    {
        var coins = await _portfolioService.Context.Coins.AsNoTracking().ToListAsync();

        foreach (var coin in coins)
        {
            await _indicatorService.CalculateMaAsync(coin);
        }
    }


    public async Task<List<Coin>> GetTopWinners()
    {
        try
        {
            // Server-side query: alleen de top-5 kolommen ophalen i.p.v. alle Assets laden
            return await _portfolioService.Context.Assets
                .AsNoTracking()
                .Where(x => x.Qty > 0 && x.Coin.Change24Hr > 0)
                .Select(x => x.Coin)
                .Distinct()
                .OrderByDescending(c => c.Change24Hr)
                .Take(5)
                .ToListAsync();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<Coin>> GetTopLosers()
    {
        try
        {
            return await _portfolioService.Context.Assets
                .AsNoTracking()
                .Where(x => x.Qty > 0 && x.Coin.Change24Hr < 0)
                .Select(x => x.Coin)
                .Distinct()
                .OrderBy(c => c.Change24Hr)
                .Take(5)
                .ToListAsync();
        }
        catch
        {
            return new();
        }
    }


    public Coin GetPriceLevelsFromContext(Coin coin)
    {
        return coin;
    }

    public async Task EvaluatePriceLevels()
    {
        var coins = _portfolioService.Context.Coins
            .AsNoTracking()
            .ToList();

        foreach (var coin in coins)
        {
            _indicatorService.EvaluatePriceLevels(coin, coin.Price);
        }
    }



    public double GetPortfolioValue()
    {
        return _assetService.GetTotalsAssetsValue();
    }

    public double GetCostBase()
    {
        return _assetService.GetTotalsAssetsCostBase();
    }


    public async Task<ObservableCollection<PiePoint>> GetPiePoints(string pieChartName)
    {
        // var values = new ObservableCollection<ObservablePoint>();
        int index = 0;

        var piePoints = new ObservableCollection<PiePoint>();

        try
        {
            var portfolioValue = _assetService.GetTotalsAssetsValue();
            //minimum value visible in Pie is 0.01%. if it is 0.00 % it will be shown on every pie
            // which we don't want.

            var threshold = Math.Ceiling(0.0001 * portfolioValue);

            if (pieChartName == "PortfolioPie")
            {
                // first get TOP 10 coins based on Rank
                var assets = (await _assetService.PopulateAssetTotalsList())
                    .Where(x => x.MarketValue > threshold)
                    .OrderBy(x => x.Coin.Rank)
                    .Take(_appSettings.MaxPieCoins)
                    .ToList();
                var sumOthersMarketValue = portfolioValue - assets.Sum(x => x.MarketValue);

                foreach (var asset in assets)
                {

                    if (asset is null || portfolioValue == 0) { continue; }

                    var perc = 100 * asset.MarketValue / portfolioValue;


                    if (!double.IsInfinity(perc))
                    {
                        var piePoint = new PiePoint
                        {
                            Value = perc,
                            Label = asset.Coin.Symbol
                        };
                        piePoints.Add(piePoint);
                        index += 1;
                    }

                }
                var percOthers = 100 * sumOthersMarketValue / portfolioValue;
                if (percOthers > threshold)
                {
                    var piePoint2 = new PiePoint
                    {
                        Value = percOthers,
                        Label = "OTHERS"
                    };
                    piePoints.Add(piePoint2);
                }
            }
            if (pieChartName == "AccountsPie")
            {
                var accounts = (await _accountService.PopulateAccountsList())
                    .Where(x => x.TotalValue > threshold)
                    .OrderBy(x => x.TotalValue)
                    .ToList();

                foreach (var account in accounts)
                {
                    if (account is null || portfolioValue == 0) { continue; }

                    var perc = 100 * account.TotalValue / portfolioValue;

                    if (!double.IsInfinity(perc))
                    {
                        var piePoint = new PiePoint
                        {
                            Value = perc,
                            Label = account.Name
                        };
                        piePoints.Add(piePoint);
                        index += 1;
                    }

                }
            }
            if (pieChartName == "NarrativesPie")
            {
                var narratives = (await _narrativeService.PopulateNarrativesList())
                    .Where(x => x.TotalValue > threshold)
                    .OrderBy(x => x.TotalValue)
                    .ToList();

                foreach (var narrative in narratives)
                {
                    if (narrative is null || portfolioValue == 0) { continue; }

                    var perc = 100 * narrative.TotalValue / portfolioValue;

                    if (!double.IsInfinity(perc))
                    {
                        var piePoint = new PiePoint
                        {
                            Value = perc,
                            Label = narrative.Name
                        };
                        piePoints.Add(piePoint);
                        index += 1;
                    }

                }
            }
        }
        catch (Exception)
        {
            throw;
        }

        return piePoints;
    }



    public async Task<List<CapitalFlowPoint>> GetYearlyMutationsByTransactionKind(TransactionKind transactionKind)
    {
        var context = _portfolioService.Context;
        var dataPoints = new List<CapitalFlowPoint>();

        var mutations = await context.Mutations
            .AsNoTracking()
            .Where(x => x.Type == transactionKind)
            .Include(t => t.Transaction)
            .GroupBy(g => g.Transaction.TimeStamp.Year)
            .Select(grouped => new
            {
                Year = grouped.Key.ToString(),
                Value = grouped.Sum(m => m.Qty * m.Price),
            })
            .OrderBy(t => t.Year)
            .ToListAsync();

        foreach (var mutation in mutations)
        {
            var dataPoint = new CapitalFlowPoint();
            dataPoint.Year = mutation.Year;
            dataPoint.Value = mutation.Value;
            dataPoints.Add(dataPoint);
        }

        return dataPoints;
    }

    public Portfolio GetPortfolio()
    {
        return _portfolioService.CurrentPortfolio;
    }

    public Task<FearGreedReading?> GetFearGreedAsync()
    {
        return _fearGreedService.GetCurrentAsync();
    }
}





 
