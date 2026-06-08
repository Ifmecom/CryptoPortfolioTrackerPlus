using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface IWatchedSetupService
{
    /// <summary>Returns all setups, newest first.</summary>
    Task<List<WatchedSetup>> GetAllAsync();

    /// <summary>Returns only setups with Status == Watching.</summary>
    Task<List<WatchedSetup>> GetWatchingAsync();

    /// <summary>Persists a new setup (Id is set by EF on return).</summary>
    Task AddAsync(WatchedSetup setup);

    /// <summary>
    /// Checks every Watching setup against the supplied price map and automatically
    /// transitions to Won (TP1 hit) or Lost (SL hit).  Returns the number of updated rows.
    /// </summary>
    Task<int> AutoUpdateStatusesAsync(IReadOnlyDictionary<string, double> priceByApiId);

    /// <summary>Manually expires a setup (user dismissed it).</summary>
    Task ExpireAsync(int id);

    /// <summary>Permanently removes a setup from the database.</summary>
    Task RemoveAsync(int id);

    /// <summary>
    /// Returns true if an active (Watching or Open) setup for this coin+direction already exists.
    /// Used to prevent duplicate entries.
    /// </summary>
    Task<bool> ExistsAsync(string coinApiId, string direction);

    /// <summary>
    /// Manually closes a setup as Won or Lost at the supplied close price.
    /// Used for setups where TP/SL was reached outside of the auto-check cycle,
    /// or where the user wants to mark the outcome themselves.
    /// </summary>
    Task CloseManuallyAsync(int id, WatchedSetupStatus outcome, double closePrice);

    /// <summary>Aggregate win-rate statistics for the dashboard.</summary>
    Task<SetupStats> GetStatsAsync();

    /// <summary>
    /// Returns the most recent active (Watching or Open) setup for the given coin + direction,
    /// or null when none exists.  Used to link a new paper-trade order to its originating setup.
    /// </summary>
    Task<WatchedSetup?> GetActiveSetupForCoinAsync(string coinApiId, string direction);

    /// <summary>
    /// Records a bidirectional link: sets WatchedSetup.LinkedOrderId = orderId.
    /// Call this after the ExchangeOrder has been persisted so its Id is known.
    /// </summary>
    Task LinkOrderAsync(int setupId, int orderId);

    /// <summary>
    /// Returns all closed setups within an optional date window, used for strategy statistics.
    /// When <paramref name="from"/> or <paramref name="to"/> is null the filter is not applied.
    /// </summary>
    Task<List<WatchedSetup>> GetClosedAsync(DateTime? from, DateTime? to);
}

/// <summary>Aggregated statistics for all closed setups.</summary>
public record SetupStats(int Total, int Won, int Lost, int Watching, int Open, double WinRatePct);
