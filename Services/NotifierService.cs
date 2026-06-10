using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Enums;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace CryptoPortfolioTracker.Services;

public class NotifierService : INotifierService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(Constants.SourceContextPropertyName, nameof(NotifierService).PadRight(22));

    private readonly Settings _settings;

    // Lazy client — re-created whenever the token changes
    private TelegramBotClient? _client;
    private string _clientToken = string.Empty;

    public NotifierService(Settings settings)
    {
        _settings = settings;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public async Task NotifySignalsAsync(
        IEnumerable<(Signal Signal, string Name, string Symbol)> signals,
        CancellationToken ct = default)
    {
        if (!_settings.IsTelegramEnabled) return;

        var client = GetClient();
        if (client is null) return;

        var chatId = _settings.TelegramChatId.Trim();
        if (string.IsNullOrEmpty(chatId)) return;

        var threshold    = _settings.TelegramScoreThreshold;
        var antiThreshold = 100.0 - threshold; // for Short signals (lower bound)

        foreach (var (signal, name, symbol) in signals)
        {
            if (ct.IsCancellationRequested) break;

            // Only notify for actionable directions that pass the threshold
            bool shouldNotify = signal.Direction switch
            {
                SignalDirection.Long  => signal.CombinedScore >= threshold,
                SignalDirection.Short => signal.CombinedScore <= antiThreshold,
                _                    => false,
            };

            if (!shouldNotify) continue;

            try
            {
                var message = BuildMessage(signal, name, symbol);
                await client.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
                Logger.Information("Telegram: sent signal alert for {Symbol} ({Direction}, score={Score:F1})",
                    symbol, signal.Direction, signal.CombinedScore);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Telegram: failed to send alert for {Symbol}", symbol);
            }
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var client = GetClient();
        if (client is null)
        {
            Logger.Warning("Telegram: test failed — no bot token configured");
            return false;
        }

        var chatId = _settings.TelegramChatId.Trim();
        if (string.IsNullOrEmpty(chatId))
        {
            Logger.Warning("Telegram: test failed — no chat ID configured");
            return false;
        }

        try
        {
            var me = await client.GetMeAsync(ct);
            await client.SendTextMessageAsync(
                chatId,
                $"✅ <b>Crypto Portfolio Tracker Plus</b>\n\nVerbinding geslaagd! Bot: @{me.Username}",
                parseMode: ParseMode.Html,
                cancellationToken: ct);

            Logger.Information("Telegram: test message sent successfully via @{BotName}", me.Username);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Telegram: test connection failed");
            return false;
        }
    }

    public async Task SendAlertAsync(string htmlMessage, CancellationToken ct = default)
    {
        if (!_settings.IsTelegramEnabled || string.IsNullOrWhiteSpace(htmlMessage)) return;

        var client = GetClient();
        if (client is null) return;

        var chatId = _settings.TelegramChatId.Trim();
        if (string.IsNullOrEmpty(chatId)) return;

        try
        {
            await client.SendTextMessageAsync(chatId, htmlMessage, parseMode: ParseMode.Html, cancellationToken: ct);
            Logger.Information("Telegram: alert verzonden");
        }
        catch (Exception ex)
        {
            // Alerts zijn best-effort — nooit het aanroepende proces laten falen.
            Logger.Warning(ex, "Telegram: alert verzenden mislukt");
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private TelegramBotClient? GetClient()
    {
        var token = _settings.TelegramBotToken.Trim();
        if (string.IsNullOrEmpty(token)) return null;

        // Re-create client only when token changes
        if (_client is null || _clientToken != token)
        {
            _client = new TelegramBotClient(token);
            _clientToken = token;
        }

        return _client;
    }

    private static string BuildMessage(Signal signal, string name, string symbol)
    {
        var directionEmoji = signal.Direction switch
        {
            SignalDirection.Long  => "🟢",
            SignalDirection.Short => "🔴",
            _                    => "⚪",
        };

        var regimeLabel = signal.MarketRegimeMultiplier switch
        {
            >= 1.0 => "RiskOn 🚀",
            <= 0.4 => "RiskOff ⚠️",
            _      => "Neutral ➡️",
        };

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"{directionEmoji} <b>{symbol} — {name}</b>");
        lines.AppendLine($"Score: <b>{signal.CombinedScore:F1}</b>  |  Richting: <b>{signal.Direction}</b>");
        lines.AppendLine($"Regime: {regimeLabel}");

        if (!string.IsNullOrWhiteSpace(signal.Reasoning))
        {
            lines.AppendLine();
            // Show first 3 lines of reasoning (avoid overly long messages)
            var reasonLines = signal.Reasoning
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Take(4);
            foreach (var line in reasonLines)
                lines.AppendLine(line.Trim());
        }

        lines.Append($"\n<i>{signal.CreatedAt:dd-MM-yyyy HH:mm} UTC</i>");
        return lines.ToString().Trim();
    }
}
