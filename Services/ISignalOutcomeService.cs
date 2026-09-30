using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>Samenvatting van één bijwerk-ronde van de signal-outcome-tracker.</summary>
public sealed record SignalOutcomeUpdateResult(
    int NewRecords,        // nieuw opgenomen signalen (één per coin per dag)
    int Measured,          // uitkomsten (deels) gemeten in deze ronde
    int Completed,         // uitkomsten die nu volledig zijn (alle horizons t/m 14 dagen)
    int WithoutData,       // uitkomsten waarvoor geen koersdata gevonden werd
    int Pending)           // nog openstaand na deze ronde
{
    public static SignalOutcomeUpdateResult Empty { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>
/// Signal-outcome-tracker (v1.46): legt vast wat de koers deed ná elk signaal van de SignalEngine en
/// elke Pattern Trading-scan, en kalibreert daarmee de scores naar een gemeten trefkans.
/// Zie <see cref="SignalOutcomeEvaluator"/> (meting) en <see cref="SignalCalibrationCalculator"/> (kalibratie).
/// </summary>
public interface ISignalOutcomeService
{
    /// <summary>
    /// Neemt nieuwe SignalEngine-signalen op (Long/Short, één per coin per dag — ook met terugwerkende
    /// kracht uit de bestaande Signals-tabel) en meet alle openstaande uitkomsten met daily klines.
    /// Faalt nooit hard: fouten worden gelogd en de ronde gaat door.
    /// </summary>
    Task<SignalOutcomeUpdateResult> UpdateAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Legt de Long/Short-richting + TradabilityScore van een Pattern Trading-scan vast als te meten
    /// signaal (één per coin per dag; alleen score ≥ setup-drempel). Faalt stil.
    /// </summary>
    Task RecordPatternScanAsync(IEnumerable<PatternCoinAnalysis> results, CancellationToken ct = default);

    /// <summary>Alle vastgelegde uitkomsten (read-only).</summary>
    Task<IReadOnlyList<SignalOutcome>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Kalibratietabel voor één bron op één horizon (zie <see cref="SignalCalibrationCalculator.Compute"/>).</summary>
    Task<IReadOnlyList<SignalCalibrationRow>> GetCalibrationAsync(
        string source, int horizonDays = SignalCalibrationCalculator.DefaultHorizonDays, CancellationToken ct = default);
}
