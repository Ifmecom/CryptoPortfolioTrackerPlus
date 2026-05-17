using CryptoPortfolioTracker.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class MarketRegimeService : IMarketRegimeService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(MarketRegimeService).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly IIndicatorService _indicatorService;

    public MarketRegimeService(PortfolioService portfolioService, IIndicatorService indicatorService)
    {
        _portfolioService = portfolioService;
        _indicatorService = indicatorService;
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
}
