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
            bool rsiBullish    = btc.Rsi is > 0 and < 65;

            var regime = (priceAboveEma, rsiBullish) switch
            {
                (true, true) => MarketRegime.RiskOn,
                (false, _)   => MarketRegime.RiskOff,
                _            => MarketRegime.Neutral,
            };

            Logger.Information(
                "MarketRegimeService: {Regime} (BTC price={Price:F0}, EMA={Ema:F0}, RSI={Rsi:F1})",
                regime, btc.Price, btc.Ema, btc.Rsi);

            return regime;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MarketRegimeService: error determining regime");
            return MarketRegime.Neutral;
        }
    }
}
