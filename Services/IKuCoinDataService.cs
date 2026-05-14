using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IKuCoinDataService
{
    /// <summary>Fetches OHLCV bars from the KuCoin public candles endpoint (no API key required).
    /// Returns empty list when the symbol is not listed or the request fails.</summary>
    Task<List<OhlcvBar>> GetKlinesAsync(string kuCoinSymbol, string interval, int limit = 200);

    /// <summary>Converts a coin symbol to a KuCoin trading pair (e.g. "PONKE" → "PONKE-USDT").</summary>
    string ResolveKuCoinSymbol(string coinSymbol);
}
