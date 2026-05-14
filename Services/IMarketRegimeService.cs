using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Services;

public interface IMarketRegimeService
{
    Task<MarketRegime> GetCurrentRegimeAsync();
}
