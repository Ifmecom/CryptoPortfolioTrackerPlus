using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface ISentimentService
{
    Task CollectAndScoreAsync(CancellationToken ct = default);
    Task<double> GetAggregatedScoreAsync(Coin coin, TimeSpan window);
}
