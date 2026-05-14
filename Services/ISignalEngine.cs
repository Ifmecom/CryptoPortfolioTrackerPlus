using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface ISignalEngine
{
    Task<List<Signal>> EvaluateAsync(Narrative? watchlist = null, CancellationToken ct = default);
    Task<Signal?> EvaluateCoinAsync(Coin coin, Timeframe tf = Timeframe.OneDay);
}
