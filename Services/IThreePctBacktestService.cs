using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Voert de backtest uit voor de 3%-trading-strategie en slaat de
/// kalibratie-resultaten op als JSON.
/// </summary>
public interface IThreePctBacktestService
{
    /// <summary>
    /// Haalt historische bars op voor het opgegeven Binance-symbool,
    /// scoort elke bar met het 5-factor model, simuleert per signaal of TP
    /// (netto +3%) of SL eerst geraakt wordt, en retourneert de gecalibreerde
    /// statistieken per scoreklasse.
    /// </summary>
    Task<List<ScoreClassCalibration>> RunAsync(
        string             binanceSymbol,
        BacktestParameters parameters,
        IProgress<(int done, int total, string status)> progress,
        CancellationToken  ct = default);

    /// <summary>Laadt de opgeslagen kalibratie-resultaten (null als nog niet aanwezig).</summary>
    List<ScoreClassCalibration>? LoadCalibration();

    /// <summary>Slaat de kalibratie-resultaten op naar JSON in AppDataPath.</summary>
    void SaveCalibration(List<ScoreClassCalibration> results);
}
