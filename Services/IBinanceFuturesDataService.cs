using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt positioneringsdata op van de Binance Futures API (fapi.binance.com):
/// funding rate, open interest en long/short-verhouding.
/// Coins die niet op de futures-markt staan, retourneren IsAvailable = false.
/// </summary>
public interface IBinanceFuturesDataService
{
    Task<FuturesPositioning> GetPositioningAsync(string binanceSymbol, CancellationToken ct = default);
}
