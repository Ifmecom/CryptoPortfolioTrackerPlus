using CryptoPortfolioTracker.Models;
using Serilog;
using System.ServiceModel.Syndication;
using System.Xml;

namespace CryptoPortfolioTracker.Services.Sentiment;

public class RssConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<RssConnector>();

    public async Task<List<RawSentimentItem>> FetchAsync(BronSource source, string coinSymbol, CancellationToken ct = default)
    {
        var items = new List<RawSentimentItem>();
        if (string.IsNullOrWhiteSpace(source.Url)) return items;

        try
        {
            using var reader = XmlReader.Create(source.Url, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Ignore,
                MaxCharactersFromEntities = 1024
            });

            var feed = await Task.Run(() => SyndicationFeed.Load(reader), ct);

            foreach (var entry in feed.Items.Take(20))
            {
                var text = (entry.Title?.Text ?? string.Empty)
                         + " " + (entry.Summary?.Text ?? string.Empty);

                // Only include items that mention the coin symbol
                if (!text.Contains(coinSymbol, StringComparison.OrdinalIgnoreCase))
                    continue;

                var published = entry.PublishDate == DateTimeOffset.MinValue
                    ? DateTime.UtcNow
                    : entry.PublishDate.UtcDateTime;

                var url = entry.Links.FirstOrDefault()?.Uri?.ToString() ?? source.Url;
                items.Add(new RawSentimentItem(text.Trim(), published, url));
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "RssConnector failed for {Url}", source.Url);
        }

        return items;
    }
}
