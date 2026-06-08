using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt globale cryptomarktdata op van CoinGecko /global:
/// BTC-dominantie, totale marktcap en volume.
/// </summary>
public interface IGlobalMarketDataService
{
    /// <summary>Geeft null als de API niet bereikbaar is.</summary>
    Task<GlobalMarketData?> GetGlobalDataAsync(CancellationToken ct = default);
}
