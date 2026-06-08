using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using Skender.Stock.Indicators;

namespace CryptoPortfolioTracker.Services;

public class MarketRegimeService : IMarketRegimeService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(MarketRegimeService).PadRight(22));

    private readonly PortfolioService         _portfolioService;
    private readonly IIndicatorService        _indicatorService;
    private readonly IBinanceDataService      _binance;
    private readonly IGlobalMarketDataService _globalMarket;

    public MarketRegimeService(
        PortfolioService         portfolioService,
        IIndicatorService        indicatorService,
        IBinanceDataService      binance,
        IGlobalMarketDataService globalMarket)
    {
        _portfolioService = portfolioService;
        _indicatorService = indicatorService;
        _binance          = binance;
        _globalMarket     = globalMarket;
    }

    public async Task<MarketRegime> GetCurrentRegimeAsync()
    {
        try
        {
            var context = _portfolioService.Context;
            if (context is null) return MarketRegime.Neutral;

            var btc = await context.Coins
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ApiId == "bitcoin");

            if (btc is null || btc.Price == 0)
            {
                Logger.Warning("MarketRegimeService: Bitcoin not found in DB, defaulting to Neutral");
                return MarketRegime.Neutral;
            }

            // Recalculate MA (→ btc.Ema) and RSI (→ btc.Rsi) via existing IndicatorService
            await _indicatorService.RecalculateAllAsync(btc);

            if (btc.Ema == 0)
            {
                Logger.Warning("MarketRegimeService: BTC EMA not available (no chart data?), defaulting to Neutral");
                return MarketRegime.Neutral;
            }

            bool priceAboveEma = btc.Price > btc.Ema;

            // RSI ≥ 80 = kritisch overbought → voorzichtig.
            // RSI = 0  = niet berekend (geen chart-data) → niet als bearish behandelen,
            //            gewoon op EMA-signaal vertrouwen.
            bool rsiOverbought = btc.Rsi > 0 && btc.Rsi >= 80;

            var regime = (priceAboveEma, rsiOverbought) switch
            {
                (true,  false) => MarketRegime.RiskOn,   // uptrend, niet extreem overbought
                (true,  true)  => MarketRegime.Neutral,  // uptrend maar RSI ≥ 80
                (false, _)     => MarketRegime.RiskOff,  // downtrend
            };

            Logger.Information(
                "MarketRegimeService: {Regime} (BTC price={Price:F0}, EMA={Ema:F0}, RSI={Rsi:F1}, overbought={Ob})",
                regime, btc.Price, btc.Ema, btc.Rsi, rsiOverbought);

            return regime;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MarketRegimeService: error determining regime");
            return MarketRegime.Neutral;
        }
    }

    // ── Sprint B: verrijkte regime-context ────────────────────────────────────

    public async Task<MarketRegimeContext> GetRegimeContextAsync(CancellationToken ct = default)
    {
        try
        {
            // Haal 250 dagelijkse BTC-candles op via Binance (echte OHLCV)
            var bars = await _binance.GetKlinesAsync("BTCUSDT", "1d", limit: 250);

            double ema50 = 0, ema200 = 0, btcRsi = 0, btcPrice = 0;
            string emaStatus = "Onvoldoende data";
            MarketRegime regime = MarketRegime.Neutral;

            if (bars.Count >= 210)
            {
                var quotes  = ThreePctScoringService.ToQuotes(bars);
                btcPrice = bars[^1].Close;

                ema50  = quotes.GetEma(50) .LastOrDefault()?.Ema  ?? 0;
                ema200 = quotes.GetEma(200).LastOrDefault()?.Ema  ?? 0;
                btcRsi = (double?)quotes.GetRsi(14).LastOrDefault()?.Rsi ?? 50;

                // EMA-status
                bool goldenCross = ema50 > ema200;
                bool priceAbove50 = btcPrice > ema50;

                emaStatus = (goldenCross, priceAbove50) switch
                {
                    (true,  true)  => "Golden Cross ↑",
                    (true,  false) => "Golden Cross — prijs onder EMA50",
                    (false, false) => "Death Cross ↓",
                    (false, true)  => "Death Cross — prijs boven EMA50",
                };

                // Regime
                bool rsiOverbought = btcRsi > 0 && btcRsi >= 80;
                regime = (priceAbove50 && goldenCross, rsiOverbought) switch
                {
                    (true,  false) => MarketRegime.RiskOn,
                    (true,  true)  => MarketRegime.Neutral,
                    (false, _)     => MarketRegime.RiskOff,
                };
            }

            // BTC dominantie
            double btcDom = 0;
            var globalData = await _globalMarket.GetGlobalDataAsync(ct);
            if (globalData is not null) btcDom = globalData.BtcDominancePct;

            var context = new MarketRegimeContext(regime, emaStatus, btcDom, btcRsi, btcPrice, ema50, ema200);

            Logger.Information(
                "MarketRegime (Sprint B): {Regime} | {Ema} | dom={Dom:0.0}% | RSI={Rsi:0.0}",
                regime, emaStatus, btcDom, btcRsi);

            return context;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Error(ex, "MarketRegimeService.GetRegimeContextAsync failed");
            return new MarketRegimeContext(
                MarketRegime.Neutral, "Fout bij ophalen", 0, 50, 0, 0, 0);
        }
    }
}
