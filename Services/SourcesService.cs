using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoPortfolioTracker.Services;

public class SourcesService : ISourcesService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(SourcesService).PadRight(22));

    private readonly PortfolioService _portfolioService;

    public SourcesService(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    public async Task<List<BronSource>> GetAllAsync()
    {
        var context = _portfolioService.Context;
        return await context.BronSources
            .AsNoTracking()
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Handle)
            .ToListAsync();
    }

    public async Task AddAsync(BronSource source)
    {
        var context = _portfolioService.Context;
        context.BronSources.Add(source);
        await context.SaveChangesAsync();
        Logger.Information("BronSource added: {Type} {Handle}", source.Type, source.Handle);
    }

    public async Task UpdateAsync(BronSource source)
    {
        var context = _portfolioService.Context;
        var existing = await context.BronSources.FindAsync(source.Id);
        if (existing is null) return;

        existing.Type             = source.Type;
        existing.Handle           = source.Handle;
        existing.Url              = source.Url;
        existing.ReliabilityScore = source.ReliabilityScore;
        existing.IsActive         = source.IsActive;

        await context.SaveChangesAsync();
        Logger.Information("BronSource updated: {Id} {Handle}", source.Id, source.Handle);
    }

    public async Task DeleteAsync(BronSource source)
    {
        var context = _portfolioService.Context;
        var existing = await context.BronSources.FindAsync(source.Id);
        if (existing is null) return;

        context.BronSources.Remove(existing);
        await context.SaveChangesAsync();
        Logger.Information("BronSource deleted: {Id} {Handle}", source.Id, source.Handle);
    }

    public async Task ToggleActiveAsync(BronSource source)
    {
        var context = _portfolioService.Context;
        var existing = await context.BronSources.FindAsync(source.Id);
        if (existing is null) return;

        existing.IsActive = !existing.IsActive;
        await context.SaveChangesAsync();
        Logger.Information("BronSource toggled: {Id} IsActive={IsActive}", existing.Id, existing.IsActive);
    }

    public async Task SeedDefaultsIfEmptyAsync()
    {
        var context = _portfolioService.Context;
        if (await context.BronSources.AnyAsync()) return;

        var defaults = new List<BronSource>
        {
            // ── Reddit ──────────────────────────────────────────────────────
            new() { Type = SentimentSource.Reddit,      Handle = "r/CryptoCurrency",   Url = "https://www.reddit.com/r/CryptoCurrency/",   ReliabilityScore = 0.70, IsActive = true  },
            new() { Type = SentimentSource.Reddit,      Handle = "r/Altstreetbets",     Url = "https://www.reddit.com/r/Altstreetbets/",     ReliabilityScore = 0.55, IsActive = true  },
            new() { Type = SentimentSource.Reddit,      Handle = "r/Bitcoin",           Url = "https://www.reddit.com/r/Bitcoin/",           ReliabilityScore = 0.75, IsActive = true  },
            new() { Type = SentimentSource.Reddit,      Handle = "r/ethereum",          Url = "https://www.reddit.com/r/ethereum/",          ReliabilityScore = 0.70, IsActive = true  },
            new() { Type = SentimentSource.Reddit,      Handle = "r/CryptoMarkets",     Url = "https://www.reddit.com/r/CryptoMarkets/",     ReliabilityScore = 0.60, IsActive = true  },
            new() { Type = SentimentSource.Reddit,      Handle = "r/defi",              Url = "https://www.reddit.com/r/defi/",              ReliabilityScore = 0.60, IsActive = false },
            new() { Type = SentimentSource.Reddit,      Handle = "r/solana",            Url = "https://www.reddit.com/r/solana/",            ReliabilityScore = 0.65, IsActive = false },

            // ── RSS ─────────────────────────────────────────────────────────
            new() { Type = SentimentSource.Rss,         Handle = "CoinDesk",            Url = "https://www.coindesk.com/arc/outboundfeeds/rss/",          ReliabilityScore = 0.80, IsActive = true  },
            new() { Type = SentimentSource.Rss,         Handle = "Cointelegraph",       Url = "https://cointelegraph.com/rss",                            ReliabilityScore = 0.75, IsActive = true  },
            new() { Type = SentimentSource.Rss,         Handle = "Decrypt",             Url = "https://decrypt.co/feed",                                  ReliabilityScore = 0.75, IsActive = true  },
            new() { Type = SentimentSource.Rss,         Handle = "The Block",           Url = "https://www.theblock.co/rss.xml",                          ReliabilityScore = 0.80, IsActive = true  },
            new() { Type = SentimentSource.Rss,         Handle = "CryptoSlate",         Url = "https://cryptoslate.com/feed/",                            ReliabilityScore = 0.70, IsActive = true  },
            new() { Type = SentimentSource.Rss,         Handle = "BeInCrypto",          Url = "https://beincrypto.com/feed/",                             ReliabilityScore = 0.65, IsActive = false },
            new() { Type = SentimentSource.Rss,         Handle = "NewsBTC",             Url = "https://www.newsbtc.com/feed/",                            ReliabilityScore = 0.60, IsActive = false },

            // ── CryptoPanic ─────────────────────────────────────────────────
            new() { Type = SentimentSource.CryptoPanic, Handle = "CryptoPanic (public)", Url = "https://cryptopanic.com/api/v1/posts/?public=true",        ReliabilityScore = 0.75, IsActive = true  },
            new() { Type = SentimentSource.CryptoPanic, Handle = "CryptoPanic (hot)",    Url = "https://cryptopanic.com/api/v1/posts/?public=true&filter=hot", ReliabilityScore = 0.70, IsActive = false },

            // ── Telegram ─────────────────────────────────────────────────────
            // Vereist WTelegramClient-authenticatie — standaard uitgeschakeld
            new() { Type = SentimentSource.Telegram,    Handle = "@cryptosignals",      Url = string.Empty,                                               ReliabilityScore = 0.50, IsActive = false },
            new() { Type = SentimentSource.Telegram,    Handle = "@whale_alert",        Url = string.Empty,                                               ReliabilityScore = 0.65, IsActive = false },
        };

        context.BronSources.AddRange(defaults);
        await context.SaveChangesAsync();
        Logger.Information("BronSources seeded with {Count} default sources", defaults.Count);
    }
}
