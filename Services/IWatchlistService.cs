using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Manages the user's Pattern-Trading watchlist.
/// Coins on the watchlist are analysed alongside portfolio holdings.
/// </summary>
public interface IWatchlistService
{
    /// <summary>Returns all watchlist entries, newest first.</summary>
    Task<List<WatchlistItem>> GetAllAsync();

    /// <summary>Adds a coin to the watchlist.  Silently ignores duplicates.</summary>
    Task AddAsync(WatchlistItem item);

    /// <summary>Removes a coin from the watchlist by its CoinGecko ApiId.</summary>
    Task RemoveAsync(string apiId);

    /// <summary>Returns true when the given ApiId is already on the watchlist.</summary>
    Task<bool> ExistsAsync(string apiId);

    /// <summary>
    /// Searches CoinGecko for coins matching <paramref name="query"/>.
    /// Returns up to 10 results sorted by market cap rank.
    /// </summary>
    Task<List<WatchlistSearchResult>> SearchCoinsAsync(string query);
}
