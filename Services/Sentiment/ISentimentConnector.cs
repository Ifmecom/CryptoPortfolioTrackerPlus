using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services.Sentiment;

/// <summary>
/// Fetches raw text items from a sentiment source.
/// Coin matching is done by the caller — connectors return all recent items.
/// </summary>
public interface ISentimentConnector
{
    /// <summary>
    /// Returns up to N recent items from this source, without coin filtering.
    /// </summary>
    Task<List<RawSentimentItem>> FetchAsync(BronSource source, CancellationToken ct = default);
}

public record RawSentimentItem(string Text, DateTime PublishedAt, string SourceUrl);
