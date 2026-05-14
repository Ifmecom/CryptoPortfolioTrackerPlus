using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IIndicatorService
{
    // Existing — must not break
    Task CalculateRsiAsync(Coin coin);
    Task<double> CalculateMaAsync(Coin coin);
    void EvaluatePriceLevels(Coin coin, double newValue);

    // PLUS — Sprint 1.2
    Task<MacdData> CalculateMacdAsync(Coin coin);
    Task<BollingerData> CalculateBollingerAsync(Coin coin);
    Task<double> CalculateAtrAsync(Coin coin);
    Task<double> CalculateStochRsiAsync(Coin coin);
    Task<TaScore> CalculateTaScoreAsync(Coin coin, Timeframe tf);
    Task RecalculateAllAsync(Coin coin);
    Task CalculateExtendedIndicatorsAsync(Coin coin);
}
