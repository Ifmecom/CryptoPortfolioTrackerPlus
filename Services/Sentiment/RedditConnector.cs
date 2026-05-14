using CryptoPortfolioTracker.Models;
using Flurl.Http;
using Newtonsoft.Json;
using Serilog;

namespace CryptoPortfolioTracker.Services.Sentiment;

/// <summary>
/// Reads public subreddit posts via Reddit's anonymous JSON API.
/// No API key required for public subreddits.
/// Handle format: "r/CryptoCurrency" or just "CryptoCurrency".
/// </summary>
public class RedditConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<RedditConnector>();
    private const string BaseUrl = "https://www.reddit.com";

    // Cache: subreddit → (fetchedAt, items)
    private static readonly Dictionary<string, (DateTime At, List<RawSentimentItem> Items)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(14);

    public async Task<List<RawSentimentItem>> FetchAsync(BronSource source, CancellationToken ct = default)
    {
        var subreddit = ParseSubreddit(source.Handle);
        if (string.IsNullOrWhiteSpace(subreddit)) return [];

        if (_cache.TryGetValue(subreddit, out var cached) && DateTime.UtcNow - cached.At < CacheTtl)
            return cached.Items;

        var items = new List<RawSentimentItem>();
        try
        {
            var url = $"{BaseUrl}/r/{subreddit}/new.json?limit=50";
            var json = await url
                .WithHeader("User-Agent", "CryptoFolioTrackerPlus/1.4 (personal tool)")
                .WithTimeout(15)
                .GetStringAsync(cancellationToken: ct);

            var response = JsonConvert.DeserializeObject<RedditListing>(json);
            if (response?.Data?.Children is null) return items;

            foreach (var child in response.Data.Children)
            {
                var post = child.Data;
                if (post is null) continue;

                // Combine title + selftext for richer matching surface
                var text = string.Join(" ",
                    post.Title ?? string.Empty,
                    post.Selftext ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(text)) continue;

                // Skip removed/deleted posts
                if (text == "[removed]" || text == "[deleted]") continue;

                var published = DateTimeOffset.FromUnixTimeSeconds((long)(post.CreatedUtc ?? 0)).UtcDateTime;
                var postUrl   = string.IsNullOrEmpty(post.Permalink)
                    ? $"https://reddit.com/r/{subreddit}"
                    : $"https://reddit.com{post.Permalink}";

                items.Add(new RawSentimentItem(text, published, postUrl));
            }

            _cache[subreddit] = (DateTime.UtcNow, items);
            Logger.Debug("RedditConnector: r/{Subreddit} — {Count} items fetched", subreddit, items.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "RedditConnector failed for r/{Subreddit}", subreddit);
        }

        return items;
    }

    private static string ParseSubreddit(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle)) return string.Empty;
        // Accept "r/CryptoCurrency", "/r/CryptoCurrency", or just "CryptoCurrency"
        var trimmed = handle.TrimStart('/');
        if (trimmed.StartsWith("r/", StringComparison.OrdinalIgnoreCase))
            return trimmed[2..];
        return trimmed;
    }

    // ── JSON models ───────────────────────────────────────────────────────────

    private class RedditListing
    {
        [JsonProperty("data")] public RedditListingData? Data { get; set; }
    }
    private class RedditListingData
    {
        [JsonProperty("children")] public List<RedditChild>? Children { get; set; }
    }
    private class RedditChild
    {
        [JsonProperty("data")] public RedditPost? Data { get; set; }
    }
    private class RedditPost
    {
        [JsonProperty("title")]       public string?  Title       { get; set; }
        [JsonProperty("selftext")]    public string?  Selftext    { get; set; }
        [JsonProperty("permalink")]   public string?  Permalink   { get; set; }
        [JsonProperty("created_utc")] public double?  CreatedUtc  { get; set; }
    }
}
