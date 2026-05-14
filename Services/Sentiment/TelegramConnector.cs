using CryptoPortfolioTracker.Models;
using Serilog;

namespace CryptoPortfolioTracker.Services.Sentiment;

// Stub — requires WTelegramClient NuGet + Telegram API credentials (api_id, api_hash, phone).
// Activate in Phase 2: add WTelegramClient package, configure credentials in Settings,
// implement channel history fetch and message filtering per coinSymbol.
public class TelegramConnector : ISentimentConnector
{
    private static readonly ILogger Logger = Log.Logger.ForContext<TelegramConnector>();

    public Task<List<RawSentimentItem>> FetchAsync(BronSource source, CancellationToken ct = default)
    {
        Logger.Debug("TelegramConnector: not yet configured, skipping {Handle}", source.Handle);
        return Task.FromResult(new List<RawSentimentItem>());
    }
}
