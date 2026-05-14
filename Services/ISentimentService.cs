using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface ISentimentService
{
    /// <summary>Starts the 15-minute background collection timer.</summary>
    void Start();

    /// <summary>Stops the background timer.</summary>
    void Stop();

    /// <summary>Runs a full collection cycle immediately (also used by the background timer).</summary>
    Task RunNowAsync(CancellationToken ct = default);

    /// <summary>Returns the weighted-average sentiment score for a coin over the given window.</summary>
    Task<double> GetAggregatedScoreAsync(Coin coin, TimeSpan window);

    // ── Observable state for the UI ──────────────────────────────────────────
    bool      IsCollecting   { get; }
    DateTime? LastRunAt      { get; }
    string    LastRunStatus  { get; }

    event EventHandler? StateChanged;
}
