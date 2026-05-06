using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services.Sentiment;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class SentimentService : ISentimentService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(SentimentService).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly Dictionary<SentimentSource, ISentimentConnector> _connectors;

    public SentimentService(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
        _connectors = new Dictionary<SentimentSource, ISentimentConnector>
        {
            [SentimentSource.Rss]          = new RssConnector(),
            [SentimentSource.CryptoPanic]  = new CryptoPanicConnector(),
            [SentimentSource.Reddit]       = new RedditConnector(),
            [SentimentSource.Telegram]     = new TelegramConnector(),
        };
    }

    public async Task CollectAndScoreAsync(CancellationToken ct = default)
    {
        var context = _portfolioService.Context;
        Logger.Information("SentimentService: starting collection run");

        var sources = await context.BronSources
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        var coins = await context.Coins
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var coin in coins)
        {
            if (ct.IsCancellationRequested) break;

            var readings = new List<SentimentReading>();

            foreach (var source in sources)
            {
                if (!_connectors.TryGetValue(source.Type, out var connector)) continue;

                var items = await connector.FetchAsync(source, coin.Symbol, ct);
                foreach (var item in items)
                {
                    var score      = SentimentAnalyzer.Score(item.Text);
                    var confidence = SentimentAnalyzer.Confidence(item.Text);

                    readings.Add(new SentimentReading
                    {
                        CoinId         = coin.Id,
                        Source         = source.Type,
                        SentimentScore = score,
                        Confidence     = confidence,
                        MentionCount   = 1,
                        Timestamp      = item.PublishedAt,
                        RawSnippet     = item.Text.Length > 2000 ? item.Text[..2000] : item.Text,
                    });
                }
            }

            if (readings.Count > 0)
            {
                context.SentimentReadings.AddRange(readings);
                await context.SaveChangesAsync(ct);

                await UpdateCoinSentimentScoreAsync(coin.Id, ct);
                Logger.Information("SentimentService: {Symbol} — {Count} readings saved", coin.Symbol, readings.Count);
            }
        }

        Logger.Information("SentimentService: collection run complete");
    }

    public async Task<double> GetAggregatedScoreAsync(Coin coin, TimeSpan window)
    {
        var context = _portfolioService.Context;
        var since = DateTime.UtcNow - window;

        var readings = await context.SentimentReadings
            .AsNoTracking()
            .Where(r => r.CoinId == coin.Id && r.Timestamp >= since)
            .ToListAsync();

        if (!readings.Any()) return 0;

        // Weighted average: higher confidence = higher weight
        var weightedSum = readings.Sum(r => r.SentimentScore * Math.Max(r.Confidence, 0.1));
        var totalWeight = readings.Sum(r => Math.Max(r.Confidence, 0.1));
        return totalWeight == 0 ? 0 : Math.Round(weightedSum / totalWeight, 4);
    }

    private async Task UpdateCoinSentimentScoreAsync(int coinId, CancellationToken ct)
    {
        var context = _portfolioService.Context;
        var since = DateTime.UtcNow - TimeSpan.FromHours(24);

        var readings = await context.SentimentReadings
            .AsNoTracking()
            .Where(r => r.CoinId == coinId && r.Timestamp >= since)
            .ToListAsync(ct);

        if (!readings.Any()) return;

        var weightedSum = readings.Sum(r => r.SentimentScore * Math.Max(r.Confidence, 0.1));
        var totalWeight = readings.Sum(r => Math.Max(r.Confidence, 0.1));
        var aggregated = totalWeight == 0 ? 0 : Math.Round(weightedSum / totalWeight, 4);

        var coin = await context.Coins.FindAsync(new object[] { coinId }, ct);
        if (coin is null) return;

        coin.LatestSentimentScore = aggregated;
        context.Coins.Update(coin);
        await context.SaveChangesAsync(ct);
    }
}
