using CryptoPortfolioTracker.Models;
using Flurl.Http;
using Newtonsoft.Json;
using Serilog;

namespace CryptoPortfolioTracker.Services.Sentiment;

public class CryptoPanicConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<CryptoPanicConnector>();
    private const string BaseUrl = "https://cryptopanic.com/api/free/v1/posts/";

    public async Task<List<RawSentimentItem>> FetchAsync(BronSource source, string coinSymbol, CancellationToken ct = default)
    {
        var items = new List<RawSentimentItem>();
        try
        {
            var response = await new Flurl.Url(BaseUrl)
                .SetQueryParam("auth_token", "anonymous")
                .SetQueryParam("currencies", coinSymbol.ToUpperInvariant())
                .SetQueryParam("kind", "news")
                .WithHeader("User-Agent", "CryptoFolioTrackerPlus/1.0")
                .GetStringAsync(cancellationToken: ct);

            var result = JsonConvert.DeserializeObject<CryptoPanicResponse>(response);
            if (result?.Results is null) return items;

            foreach (var post in result.Results.Take(20))
            {
                var text = post.Title ?? string.Empty;
                var published = post.PublishedAt ?? DateTime.UtcNow;
                items.Add(new RawSentimentItem(text, published, post.Url ?? string.Empty));
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "CryptoPanicConnector failed for {Symbol}", coinSymbol);
        }
        return items;
    }

    private class CryptoPanicResponse
    {
        [JsonProperty("results")] public List<CryptoPanicPost>? Results { get; set; }
    }

    private class CryptoPanicPost
    {
        [JsonProperty("title")]        public string?   Title       { get; set; }
        [JsonProperty("url")]          public string?   Url         { get; set; }
        [JsonProperty("published_at")] public DateTime? PublishedAt { get; set; }
    }
}
