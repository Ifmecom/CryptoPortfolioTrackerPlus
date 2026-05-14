using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IBinanceDataService
{
    /// <summary>Fetches OHLCV bars from the Binance public klines endpoint (no API key required).
    /// Returns empty list when the symbol is not listed or the request fails.</summary>
    Task<List<OhlcvBar>> GetKlinesAsync(string binanceSymbol, string interval, int limit = 200);

    /// <summary>Resolves a CoinGecko ApiId + coin symbol to a Binance USDT trading pair.</summary>
    string ResolveBinanceSymbol(string coinApiId, string coinSymbol);
}
