using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IMarketRegimeService
{
    /// <summary>Huidig regime op basis van BTC EMA + RSI (bestaande methode).</summary>
    Task<MarketRegime> GetCurrentRegimeAsync();

    /// <summary>
    /// Verrijkt regime-context (Sprint B): EMA50/200-crossover + BTC dominantie.
    /// Gebruikt Binance klines voor BTC in plaats van de lokale MarketChart JSON,
    /// zodat EMA200 en echte OHLCV beschikbaar zijn.
    /// </summary>
    Task<MarketRegimeContext> GetRegimeContextAsync(CancellationToken ct = default);
}
