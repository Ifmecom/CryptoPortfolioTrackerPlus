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
}
