using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services.Sentiment;

public interface ISentimentConnector
{
    Task<List<RawSentimentItem>> FetchAsync(BronSource source, string coinSymbol, CancellationToken ct = default);
}

public record RawSentimentItem(string Text, DateTime PublishedAt, string SourceUrl);
