using System.Collections.Generic;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare evaluatie van de risk-guardrails vóór het plaatsen van een nieuwe trade.
/// Blokkerend: kill-switch, max open posities bereikt, dagelijkse verlieslimiet bereikt.
/// Een limiet van 0 betekent "niet ingesteld" en blokkeert nooit.
/// </summary>
public static class GuardrailEvaluator
{
    public static GuardrailVerdict Evaluate(
        bool   killSwitchActive,
        int    openPositions,
        int    maxOpenPositions,
        double dayRealizedPnl,
        double dailyLossLimitUsd)
    {
        var reasons = new List<string>();

        if (killSwitchActive)
            reasons.Add("Kill-switch is actief (Instellingen → Risk-guardrails).");

        if (maxOpenPositions > 0 && openPositions >= maxOpenPositions)
            reasons.Add($"Maximaal aantal open posities bereikt ({openPositions}/{maxOpenPositions}).");

        if (dailyLossLimitUsd > 0 && dayRealizedPnl <= -dailyLossLimitUsd)
            reasons.Add($"Dagelijkse verlieslimiet bereikt ({dayRealizedPnl:0.00} USDT ≤ -{dailyLossLimitUsd:0.00} USDT) — handelen geblokkeerd voor de rest van de dag.");

        return reasons.Count == 0 ? GuardrailVerdict.Allowed : new GuardrailVerdict(true, reasons);
    }
}
