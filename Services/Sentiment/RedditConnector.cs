using CryptoPortfolioTracker.Models;
using Serilog;

namespace CryptoPortfolioTracker.Services.Sentiment;

// Stub — requires Reddit API credentials (client_id + client_secret) configured in Settings.
// Activate in Phase 2 by implementing OAuth2 flow via Flurl and the Reddit JSON API.
public class RedditConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<RedditConnector>();

    public Task<List<RawSentimentItem>> FetchAsync(BronSource source, string coinSymbol, CancellationToken ct = default)
    {
        Logger.Debug("RedditConnector: not yet configured, skipping {Handle}", source.Handle);
        return Task.FromResult(new List<RawSentimentItem>());
    }
}
