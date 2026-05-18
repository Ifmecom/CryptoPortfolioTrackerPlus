using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using CryptoPortfolioTracker.Services.Sentiment;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Background service that collects sentiment from all active sources every 15 minutes.
/// Uses a batch approach: each source is fetched ONCE per run, then all coins are matched
/// in-memory — avoiding the old N-coins × M-sources API call explosion.
/// </summary>
public class SentimentService : ISentimentService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(SentimentService).PadRight(22));
    private static readonly TimeSpan CollectInterval  = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReadingsRetention = TimeSpan.FromDays(7);

    private readonly PortfolioService _portfolioService;
    private readonly Dictionary<SentimentSource, ISentimentConnector> _connectors;

    private CancellationTokenSource? _cts;
    private Task?                    _backgroundTask;

    // ── Observable state ─────────────────────────────────────────────────────
    public bool      IsCollecting  { get; private set; }
    public DateTime? LastRunAt     { get; private set; }
    public string    LastRunStatus { get; private set; } = "Nog niet gestart";

    public event EventHandler? StateChanged;

    // ─────────────────────────────────────────────────────────────────────────

    public SentimentService(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
        _connectors = new Dictionary<SentimentSource, ISentimentConnector>
        {
            [SentimentSource.Rss]         = new RssConnector(),
            [SentimentSource.CryptoPanic] = new CryptoPanicConnector(),
            [SentimentSource.Reddit]      = new RedditConnector(),
            [SentimentSource.Telegram]    = new TelegramConnector(),
        };
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        if (_backgroundTask is { IsCompleted: false }) return;

        _cts = new CancellationTokenSource();
        _backgroundTask = RunLoopAsync(_cts.Token);
        Logger.Information("SentimentService: background timer started (interval {Min} min)", CollectInterval.TotalMinutes);
    }

    public void Stop()
    {
        _cts?.Cancel();
        Logger.Information("SentimentService: background timer stopped");
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // First run immediately, then every 15 minutes
        await RunNowAsync(ct);

        using var timer = new PeriodicTimer(CollectInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await RunNowAsync(ct);
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
    }

    // ── Main collection run ───────────────────────────────────────────────────

    public async Task RunNowAsync(CancellationToken ct = default)
    {
        if (IsCollecting)
        {
            Logger.Debug("SentimentService: run skipped — already collecting");
            return;
        }

        IsCollecting = true;
        NotifyStateChanged();

        try
        {
            await CollectAsync(ct);
            await PruneOldReadingsAsync(ct);
        }
        catch (OperationCanceledException) { /* Graceful shutdown */ }
        catch (Exception ex)
        {
            Logger.Error(ex, "SentimentService: unhandled error during collection run");
            LastRunStatus = $"Fout: {ex.Message}";
        }
        finally
        {
            IsCollecting  = false;
            LastRunAt     = DateTime.UtcNow;
            NotifyStateChanged();
        }
    }

    // ── Batch collection ──────────────────────────────────────────────────────

    private async Task CollectAsync(CancellationToken ct)
    {
        var context = _portfolioService.Context;
        if (context is null)
        {
            LastRunStatus = "Geen portfolio geladen";
            Logger.Debug("SentimentService: no portfolio context — run skipped");
            return;
        }

        Logger.Information("SentimentService: collection run started");

        var sources = await context.BronSources
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        if (sources.Count == 0)
        {
            LastRunStatus = "Geen actieve bronnen";
            return;
        }

        var coins = await context.Coins.AsNoTracking().ToListAsync(ct);
        if (coins.Count == 0)
        {
            LastRunStatus = "Geen coins in portfolio";
            return;
        }

        // symbol → coinId  (e.g. "BTC" → 1), word-boundary matched
        var symbolMap = coins
            .GroupBy(c => c.Symbol.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Lowercase first-word of name → coinId  (e.g. "bitcoin" → 1, min 4 chars)
        var nameMap = coins
            .GroupBy(c => c.Name.Split(' ')[0].ToLowerInvariant())
            .Where(g => g.Key.Length >= 4)
            .ToDictionary(g => g.Key, g => g.First().Id);

        int totalReadings = 0;

        foreach (var source in sources)
        {
            if (ct.IsCancellationRequested) break;
            if (!_connectors.TryGetValue(source.Type, out var connector)) continue;

            var items = await connector.FetchAsync(source, ct);
            if (items.Count == 0) continue;

            var readings = new List<SentimentReading>();

            foreach (var item in items)
            {
                var upperText = item.Text.ToUpperInvariant();
                var lowerText = item.Text.ToLowerInvariant();

                var mentionedCoinIds = new HashSet<int>();

                foreach (var (sym, coinId) in symbolMap)
                    if (ContainsSymbol(upperText, sym))
                        mentionedCoinIds.Add(coinId);

                foreach (var (namePart, coinId) in nameMap)
                    if (lowerText.Contains(namePart))
                        mentionedCoinIds.Add(coinId);

                if (mentionedCoinIds.Count == 0) continue;

                var score      = SentimentAnalyzer.Score(item.Text);
                var confidence = SentimentAnalyzer.Confidence(item.Text);
                var snippet    = item.Text.Length > 2000 ? item.Text[..2000] : item.Text;

                foreach (var coinId in mentionedCoinIds)
                {
                    readings.Add(new SentimentReading
                    {
                        CoinId         = coinId,
                        Source         = source.Type,
                        SentimentScore = score,
                        Confidence     = confidence,
                        MentionCount   = 1,
                        Timestamp      = item.PublishedAt,
                        RawSnippet     = snippet,
                    });
                }
            }

            if (readings.Count > 0)
            {
                context.SentimentReadings.AddRange(readings);
                totalReadings += readings.Count;
                Logger.Debug("SentimentService: {Handle} → {Count} readings staged", source.Handle, readings.Count);
            }
        }

        // Eén SaveChangesAsync voor alle bronnen — was eerder per bron (4× losse transacties)
        if (totalReadings > 0)
            await context.SaveChangesAsync(ct);

        await UpdateCoinSentimentScoresAsync(context, coins, ct);

        LastRunStatus = $"{totalReadings} readings — {DateTime.Now:HH:mm}";
        Logger.Information("SentimentService: run complete — {Total} readings saved", totalReadings);
    }

    // ── Score aggregation ─────────────────────────────────────────────────────

    public async Task<double> GetAggregatedScoreAsync(Coin coin, TimeSpan window)
    {
        var context = _portfolioService.Context;
        if (context is null) return 0;

        var since = DateTime.UtcNow - window;

        var readings = await context.SentimentReadings
            .AsNoTracking()
            .Where(r => r.CoinId == coin.Id && r.Timestamp >= since)
            .ToListAsync();

        return Aggregate(readings);
    }

    private static double Aggregate(List<SentimentReading> readings)
    {
        if (readings.Count == 0) return 0;
        var weightedSum = readings.Sum(r => r.SentimentScore * Math.Max(r.Confidence, 0.1));
        var totalWeight = readings.Sum(r => Math.Max(r.Confidence, 0.1));
        return totalWeight == 0 ? 0 : Math.Round(weightedSum / totalWeight, 4);
    }

    private static async Task UpdateCoinSentimentScoresAsync(
        PortfolioContext context, List<Coin> coins, CancellationToken ct)
    {
        var since = DateTime.UtcNow - TimeSpan.FromHours(24);

        var allRecent = await context.SentimentReadings
            .AsNoTracking()
            .Where(r => r.Timestamp >= since)
            .ToListAsync(ct);

        if (allRecent.Count == 0) return;

        var byCoins = allRecent.GroupBy(r => r.CoinId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var coin in coins)
        {
            if (!byCoins.TryGetValue(coin.Id, out var readings)) continue;
            var score = Aggregate(readings);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE Coins SET LatestSentimentScore = {0} WHERE Id = {1}", score, coin.Id);
        }
    }

    // ── Pruning ───────────────────────────────────────────────────────────────

    private async Task PruneOldReadingsAsync(CancellationToken ct)
    {
        var context = _portfolioService.Context;
        if (context is null) return;

        var cutoff  = DateTime.UtcNow - ReadingsRetention;
        var deleted = await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM SentimentReadings WHERE Timestamp < {0}", cutoff);

        if (deleted > 0)
            Logger.Information("SentimentService: pruned {Count} old readings (>{Days}d)", deleted, ReadingsRetention.TotalDays);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool ContainsSymbol(string upperText, string symbol)
    {
        int idx = 0;
        while ((idx = upperText.IndexOf(symbol, idx, StringComparison.Ordinal)) >= 0)
        {
            bool leftOk  = idx == 0                               || !char.IsLetter(upperText[idx - 1]);
            bool rightOk = idx + symbol.Length >= upperText.Length || !char.IsLetter(upperText[idx + symbol.Length]);
            if (leftOk && rightOk) return true;
            idx += symbol.Length;
        }
        return false;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
