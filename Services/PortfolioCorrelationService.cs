using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Berekent de correlatie van de portfolio-holdings met BTC en aggregeert dit tot een
/// diversificatie-oordeel. Hergebruikt <see cref="ICorrelationService"/> (Pearson) en
/// <see cref="IBinanceDataService"/> (klines) — dezelfde bronnen als de 3%-Trading-tool.
/// </summary>
public class PortfolioCorrelationService : IPortfolioCorrelationService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(PortfolioCorrelationService).PadRight(22));

    private readonly PortfolioService    _portfolioService;
    private readonly IBinanceDataService _binance;
    private readonly ICorrelationService _correlation;

    private const int BarLimit     = 90;   // dagcandles
    private const int Lookback     = 60;   // rendementen voor Pearson
    private const int CallDelayMs  = 250;  // rate-limit tussen klines-calls

    public PortfolioCorrelationService(
        PortfolioService portfolioService,
        IBinanceDataService binance,
        ICorrelationService correlation)
    {
        _portfolioService = portfolioService;
        _binance          = binance;
        _correlation      = correlation;
    }

    public async Task<PortfolioCorrelationResult> AnalyzeAsync(
        IProgress<(int done, int total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return new PortfolioCorrelationResult();

        // Holdings met waarde > 0
        var coins = await ctx.Coins
            .Include(c => c.Assets)
            .Where(c => c.IsAsset)
            .AsNoTracking()
            .ToListAsync(ct);

        var holdings = coins
            .Select(c => new
            {
                c.ApiId, c.Symbol, c.Name, c.ImageUri, c.Price,
                Value = (c.Assets?.Sum(a => a.Qty) ?? 0) * c.Price,
            })
            .Where(h => h.Value > 0 && !string.IsNullOrEmpty(h.Symbol))
            .ToList();

        int total = holdings.Count;
        if (total == 0) return new PortfolioCorrelationResult();

        // BTC-referentiereeks éénmalig
        progress?.Report((0, total, "BTC-data ophalen…"));
        var btcBars = await _binance.GetKlinesAsync("BTCUSDT", "1d", BarLimit);

        var results = new List<CoinCorrelation>();
        int done = 0;
        foreach (var h in holdings)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            progress?.Report((done, total, $"{done}/{total} — {h.Symbol}"));

            double corr;
            bool isBtc = string.Equals(h.Symbol, "BTC", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(h.ApiId, "bitcoin", StringComparison.OrdinalIgnoreCase);
            if (isBtc)
            {
                corr = 1.0;
            }
            else
            {
                try
                {
                    var symbol = _binance.ResolveBinanceSymbol(h.ApiId ?? string.Empty, h.Symbol);
                    var bars   = await _binance.GetKlinesAsync(symbol, "1d", BarLimit);
                    corr = bars.Count >= 10 && btcBars.Count >= 10
                        ? _correlation.ComputePearson(bars, btcBars, Lookback)
                        : double.NaN;
                    await Task.Delay(CallDelayMs, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "PortfolioCorrelation: klines mislukt voor {Symbol}", h.Symbol);
                    corr = double.NaN;
                }
            }

            results.Add(new CoinCorrelation(
                h.Symbol.ToUpperInvariant(), h.Name, h.ImageUri,
                corr, h.Value, PortfolioCorrelationCalculator.Label(corr)));
        }

        return PortfolioCorrelationCalculator.Summarize(results, total);
    }
}
