using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface ITradeAnalysisService
{
    /// <summary>Generates a full multi-timeframe trade analysis for the given coin.
    /// Fetches live OHLCV data from Binance; falls back to daily JSON cache when unavailable.</summary>
    Task<TradeAnalysisResult> GenerateAsync(Coin coin);
}
