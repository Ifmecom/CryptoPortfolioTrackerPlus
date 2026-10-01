using System.Threading;
using System.Threading.Tasks;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Controleert de risk-guardrails (kill-switch, max open posities, dagelijkse verlieslimiet)
/// vóór het plaatsen van een nieuwe paper trade.
/// </summary>
public interface IGuardrailService
{
    Task<GuardrailVerdict> CheckNewTradeAsync(CancellationToken ct = default);

    /// <summary>
    /// Zelfde controle voor een live/demo-order op <paramref name="exchange"/> (v1.47): telt alleen de
    /// open posities en dag-P&amp;L van die exchange (niet de paper trades).
    /// </summary>
    Task<GuardrailVerdict> CheckNewLiveTradeAsync(Enums.ExchangeKind exchange, CancellationToken ct = default)
        => CheckNewTradeAsync(ct);
}
