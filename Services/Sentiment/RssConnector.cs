using CryptoPortfolioTracker.Models;
using Serilog;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Xml;

namespace CryptoPortfolioTracker.Services.Sentiment;

public class RssConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<RssConnector>();

    // Shared HttpClient with a browser-like User-Agent (many feeds block .NET default)
    private static readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (compatible; CryptoPT/1.0)" } },
    };

    // Simple in-process cache: url → (fetchedAt, items)
    private static readonly Dictionary<string, (DateTime At, List<RawSentimentItem> Items)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(14);

    public async Task<List<RawSentimentItem>> FetchAsync(BronSource source, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(source.Url)) return [];

        // Return cached result if still fresh
        if (_cache.TryGetValue(source.Url, out var cached) && DateTime.UtcNow - cached.At < CacheTtl)
            return cached.Items;

        var items = new List<RawSentimentItem>();
        try
        {
            // Download first via HttpClient — this throws HttpRequestException for 4xx/5xx
            // directly in our async context, so the catch below handles it cleanly.
            using var response = await _http.GetAsync(source.Url, ct);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("RssConnector: {Handle} returned HTTP {Code} — skipping",
                    source.Handle, (int)response.StatusCode);
                return items;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async         = true,
                DtdProcessing = DtdProcessing.Ignore,
                MaxCharactersFromEntities = 1024,
            });

            var feed = SyndicationFeed.Load(reader);

            foreach (var entry in feed.Items.Take(50))
            {
                var text = string.Join(" ",
                    entry.Title?.Text ?? string.Empty,
                    entry.Summary?.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(text)) continue;

                var published = entry.PublishDate == DateTimeOffset.MinValue
                    ? DateTime.UtcNow
                    : entry.PublishDate.UtcDateTime;

                var url = entry.Links.FirstOrDefault()?.Uri?.ToString() ?? source.Url;
                items.Add(new RawSentimentItem(text, published, url));
            }

            _cache[source.Url] = (DateTime.UtcNow, items);
            Logger.Debug("RssConnector: {Handle} — {Count} items fetched", source.Handle, items.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "RssConnector failed for {Url}", source.Url);
        }

        return items;
    }
}
