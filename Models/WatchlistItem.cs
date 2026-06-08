namespace CryptoPortfolioTracker.Models;

/// <summary>
/// A coin that the user is watching but does not necessarily hold.
/// Stored in the SQLite <c>WatchlistCoins</c> table via raw SQL
/// (no EF DbSet — managed by <see cref="Services.IWatchlistService"/>).
/// </summary>
public class WatchlistItem
{
    public int    Id       { get; set; }
    public string ApiId    { get; set; } = string.Empty;
    public string Name     { get; set; } = string.Empty;
    public string Symbol   { get; set; } = string.Empty;
    public string ImageUri { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight result from a CoinGecko coin search.
/// </summary>
public class WatchlistSearchResult
{
    public string ApiId    { get; set; } = string.Empty;
    public string Name     { get; set; } = string.Empty;
    public string Symbol   { get; set; } = string.Empty;
    public string ImageUri { get; set; } = string.Empty;
    public int    MarketCapRank { get; set; }
}
