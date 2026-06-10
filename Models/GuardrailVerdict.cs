using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Uitkomst van de risk-guardrail-check vóór het plaatsen van een nieuwe trade.
/// Bij <see cref="IsBlocked"/> bevat <see cref="Reasons"/> de blokkerende redenen.
/// </summary>
public record GuardrailVerdict(bool IsBlocked, IReadOnlyList<string> Reasons)
{
    public static readonly GuardrailVerdict Allowed = new(false, new List<string>());

    public string ReasonText => string.Join(" · ", Reasons);
}
