using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Infrastructure.Response.Coins;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

public class FundamentalsService : IFundamentalsService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(FundamentalsService).PadRight(22));

    private readonly PortfolioService _portfolioService;
    private readonly IDefiLlamaService _llama;
    private readonly CoinGeckoApiClient _gecko;

    // Serialiseert toegang tot de gedeelde PortfolioContext zodat gelijktijdige
    // verversingen (batch + losse analyse) geen EF-concurrency-fout veroorzaken.
    private static readonly System.Threading.SemaphoreSlim _dbGate = new(1, 1);

    // CoinGecko demo-tier: ~30 calls/min → ruime marge tussen calls bij bulk-refresh.
    private const int BulkDelayMs = 2200;

    public FundamentalsService(PortfolioService portfolioService, IDefiLlamaService llama)
    {
        _portfolioService = portfolioService;
        _llama = llama;
        _gecko = new CoinGeckoApiClient(AppConstants.ApiPath, AppConstants.CoinGeckoApiKey);
    }

    public async Task<CoinFundamentals?> RefreshAsync(string apiId, string symbol, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiId)) return null;

        CoinFullDataById data;
        try
        {
            data = await _gecko.GetCoinDetailsAsync(apiId, ct);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "FundamentalsService: ophalen mislukt voor {ApiId}", apiId);
            return null;
        }

        var f = MapToFundamentals(data, apiId, symbol, name);

        // On-chain TVL (DefiLlama) — graceful: blijft 0 als de coin geen DeFi-protocol is.
        try
        {
            var ll = await _llama.GetInfoAsync(apiId, f.Symbol, ct);
            if (ll is not null)
            {
                f.Tvl = ll.Tvl;
                f.TvlCategory = ll.Category;
            }
        }
        catch (Exception ex) { Logger.Debug(ex, "FundamentalsService: DefiLlama TVL niet beschikbaar voor {ApiId}", apiId); }

        // #6: eigen app-sentiment (Reddit/RSS) — al opgeslagen op de Coin, geen extra API-call
        try
        {
            var ctx = _portfolioService.Context;
            if (ctx is not null)
            {
                await _dbGate.WaitAsync(ct);
                try
                {
                    f.AppSentiment = await ctx.Coins.AsNoTracking()
                        .Where(c => c.ApiId == apiId)
                        .Select(c => c.LatestSentimentScore)
                        .FirstOrDefaultAsync(ct);
                }
                finally { _dbGate.Release(); }
            }
        }
        catch (Exception ex) { Logger.Debug(ex, "FundamentalsService: app-sentiment lezen mislukt voor {ApiId}", apiId); }

        FundamentalsScoreCalculator.Recompute(f, DateTime.UtcNow);
        await UpsertAsync(f, ct);
        return f;
    }

    public async Task<int> RefreshAllAsync(IProgress<(int done, int total, string status)>? progress = null, CancellationToken ct = default)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return 0;

        var coins = await ctx.Coins.AsNoTracking()
            .Where(c => c.ApiId != "")
            .Select(c => new { c.ApiId, c.Symbol, c.Name })
            .ToListAsync(ct);

        int done = 0, ok = 0;
        foreach (var c in coins)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            progress?.Report((done, coins.Count, $"{done}/{coins.Count} — {c.Symbol}"));

            var result = await RefreshAsync(c.ApiId, c.Symbol, c.Name, ct);
            if (result is not null) ok++;

            if (done < coins.Count)
                await Task.Delay(BulkDelayMs, ct);
        }
        return ok;
    }

    public async Task<List<CoinFundamentals>> GetAllAsync(CancellationToken ct = default)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return new();
        await _dbGate.WaitAsync(ct);
        try
        {
            return await ctx.Set<CoinFundamentals>().AsNoTracking()
                .OrderByDescending(x => x.TotalScore)
                .ToListAsync(ct);
        }
        finally { _dbGate.Release(); }
    }

    public async Task<CoinFundamentals?> GetAsync(string apiId, CancellationToken ct = default)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return null;
        await _dbGate.WaitAsync(ct);
        try
        {
            return await ctx.Set<CoinFundamentals>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApiId == apiId, ct);
        }
        finally { _dbGate.Release(); }
    }

    public async Task SaveDueDiligenceAsync(CoinFundamentals fundamentals, CancellationToken ct = default)
    {
        FundamentalsScoreCalculator.Recompute(fundamentals, DateTime.UtcNow);
        await UpsertAsync(fundamentals, ct);
    }

    public async Task<IReadOnlyDictionary<string, CoinFundamentals>> GetScoreMapAsync(CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        var map = new Dictionary<string, CoinFundamentals>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in all)
            if (!string.IsNullOrEmpty(f.ApiId))
                map[f.ApiId] = f;
        return map;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task UpsertAsync(CoinFundamentals f, CancellationToken ct)
    {
        var ctx = _portfolioService.Context;
        if (ctx is null) return;

        f.UpdatedAt = DateTime.UtcNow;
        await _dbGate.WaitAsync(ct);
        try
        {
            var existing = await ctx.Set<CoinFundamentals>().FirstOrDefaultAsync(x => x.ApiId == f.ApiId, ct);
            if (existing is null)
            {
                ctx.Set<CoinFundamentals>().Add(f);
            }
            else
            {
                f.Id = existing.Id;
                ctx.Entry(existing).CurrentValues.SetValues(f);
            }
            await ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "FundamentalsService: opslaan mislukt voor {ApiId}", f.ApiId);
            ctx.ChangeTracker?.Clear();
        }
        finally { _dbGate.Release(); }
    }

    private static CoinFundamentals MapToFundamentals(CoinFullDataById d, string apiId, string symbol, string name)
    {
        var md = d.MarketData;

        var f = new CoinFundamentals
        {
            ApiId  = apiId,
            Symbol = string.IsNullOrWhiteSpace(symbol) ? d.Symbol ?? "" : symbol,
            Name   = string.IsNullOrWhiteSpace(name) ? d.Name ?? "" : name,

            Categories    = d.Categories is { Count: > 0 }
                            ? string.Join(", ", d.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                            : string.Empty,
            GenesisDate   = ParseDate(d.GenesisDate),
            SentimentUpPct = d.SentimentVotesUpPercentage ?? 0,
            Description   = GetEnglish(d.Description),

            HomepageUrl   = d.Links?.Homepage?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty,
            WhitepaperUrl = d.Links?.Whitepaper ?? string.Empty,
            GithubUrl     = d.Links?.ReposUrl?.Github?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty,
            TwitterHandle = d.Links?.TwitterScreenName ?? string.Empty,
            SubredditUrl  = d.Links?.SubredditUrl ?? string.Empty,

            MarketCapRank = d.MarketCapRank ?? md?.MarketCapRank,
        };

        if (md is not null)
        {
            f.MarketCap    = Usd(md.MarketCap);
            f.Fdv          = Usd(md.FullyDilutedValuation);
            f.TotalVolume  = Usd(md.TotalVolume);
            f.Ath          = Usd(md.Ath);
            f.AthChangePct = Usd(md.AthChangePercentage);
            f.AthDate      = UsdDate(md.AthDate);
            f.Atl          = Usd(md.Atl);
            f.AtlChangePct = Usd(md.AtlChangePercentage);
            f.AtlDate      = UsdDate(md.AtlDate);

            f.CirculatingSupply = md.CirculatingSupply ?? 0;
            f.TotalSupply       = md.TotalSupply ?? 0;
            f.MaxSupply         = md.MaxSupply ?? 0;
        }

        var dev = d.DeveloperData;
        if (dev is not null)
        {
            f.GithubStars         = dev.Stars ?? 0;
            f.GithubForks         = dev.Forks ?? 0;
            f.GithubSubscribers   = dev.Subscribers ?? 0;
            f.CommitCount4Weeks   = dev.CommitCount4Weeks ?? 0;
            f.PullRequestsMerged  = dev.PullRequestsMerged ?? 0;
            f.PullRequestContribs = dev.PullRequestContributors ?? 0;
        }

        var com = d.CommunityData;
        if (com is not null)
        {
            f.TwitterFollowers  = com.TwitterFollowers ?? 0;
            f.RedditSubscribers = com.RedditSubscribers ?? 0;
            f.RedditActive48H   = com.RedditAccountsActive48H ?? 0;
        }

        return f;
    }

    private static double Usd(Dictionary<string, double?>? dict)
        => dict is not null && dict.TryGetValue("usd", out var v) && v.HasValue ? v.Value : 0;

    private static DateTime? UsdDate(Dictionary<string, DateTime?>? dict)
        => dict is not null && dict.TryGetValue("usd", out var v) ? v : null;

    private static DateTime? ParseDate(string? s)
        => DateTime.TryParse(s, out var d) ? d : null;

    private static string GetEnglish(Dictionary<string, string>? desc)
    {
        if (desc is null) return string.Empty;
        if (desc.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) return en;
        return desc.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }
}
