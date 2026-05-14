using CryptoPortfolioTracker.Models;
using Flurl.Http;
using Newtonsoft.Json;
using Serilog;

namespace CryptoPortfolioTracker.Services.Sentiment;

/// <summary>
/// Fetches recent news from the CryptoPanic free/public API.
/// No auth_token required for the public endpoint.
/// </summary>
public class CryptoPanicConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<CryptoPanicConnector>();
    private const string BaseUrl = "https://cryptopanic.com/api/free/v1/posts/";

    // Cache: url → (fetchedAt, items)
    private static readonly Dictionary<string, (DateTime At, List<RawSentimentItem> Items)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(14);

    public async Task<List<RawSentimentItem>> FetchAsync(BronSource source, CancellationToken ct = default)
    {
        var cacheKey = source.Url.Length > 0 ? source.Url : BaseUrl;

        if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.At < CacheTtl)
            return cached.Items;

        var items = new List<RawSentimentItem>();
        try
        {
            // Build URL: use source.Url if configured, otherwise fallback to public endpoint
            var endpoint = !string.IsNullOrWhiteSpace(source.Url) ? source.Url : BaseUrl;

            var json = await new Flurl.Url(endpoint)
                .SetQueryParam("public", "true")
                .SetQueryParam("kind", "news")
                .WithHeader("User-Agent", "CryptoFolioTrackerPlus/1.4 (personal tool)")
                .WithTimeout(15)
                .GetStringAsync(cancellationToken: ct);

            var result = JsonConvert.DeserializeObject<CryptoPanicResponse>(json);
            if (result?.Results is null) return items;

            foreach (var post in result.Results.Take(50))
            {
                var text = post.Title ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Include currency hints in the text so coin matching picks them up
                if (post.Currencies?.Count > 0)
                    text += " " + string.Join(" ", post.Currencies.Select(c => c.Code));

                var published = post.PublishedAt ?? DateTime.UtcNow;
                items.Add(new RawSentimentItem(text.Trim(), published, post.Url ?? string.Empty));
            }

            _cache[cacheKey] = (DateTime.UtcNow, items);
            Logger.Debug("CryptoPanicConnector: {Handle} — {Count} items fetched", source.Handle, items.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "CryptoPanicConnector failed for {Handle}", source.Handle);
        }

        return items;
    }

    // ── JSON models ───────────────────────────────────────────────────────────

    private class CryptoPanicResponse
    {
        [JsonProperty("results")] public List<CryptoPanicPost>? Results { get; set; }
    }
    private class CryptoPanicPost
    {
        [JsonProperty("title")]        public string?           Title       { get; set; }
        [JsonProperty("url")]          public string?           Url         { get; set; }
        [JsonProperty("published_at")] public DateTime?         PublishedAt { get; set; }
        [JsonProperty("currencies")]   public List<CpCurrency>? Currencies  { get; set; }
    }
    private class CpCurrency
    {
        [JsonProperty("code")] public string Code { get; set; } = string.Empty;
    }
}
