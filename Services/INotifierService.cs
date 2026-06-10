using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

public interface INotifierService
{
    /// <summary>
    /// Send Telegram alerts for a batch of evaluated signals.
    /// Only signals that pass the configured score threshold are sent.
    /// </summary>
    Task NotifySignalsAsync(IEnumerable<(Signal Signal, string Name, string Symbol)> signals,
                            CancellationToken ct = default);

    /// <summary>
    /// Send a test message to verify the bot token and chat ID are correct.
    /// Returns true on success, false on failure.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Stuurt een losse alert (HTML) naar Telegram — gebruikt voor trade-triggers
    /// (entry gevuld, TP/SL geraakt) en guardrail-events. Faalt stil wanneer Telegram
    /// uitgeschakeld of niet geconfigureerd is; gooit nooit een exception.
    /// </summary>
    Task SendAlertAsync(string htmlMessage, CancellationToken ct = default);
}
