using System;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare positiegrootte-berekening op basis van risico: bepaalt het inlegbedrag zodat
/// het verlies bij de stop-loss gelijk is aan een gekozen percentage van het kapitaal.
/// Houdt rekening met hefboom (leverage), zodat het notioneel meeschaalt met het risico.
/// </summary>
public static class PositionSizeCalculator
{
    public readonly record struct SizeResult(double Amount, double RiskAmount, double SlDistancePct, bool IsValid);

    /// <summary>
    /// Suggereert het inlegbedrag (margin) zodat verlies-bij-SL = <paramref name="riskPct"/>% van het kapitaal.
    /// </summary>
    public static SizeResult Suggest(double capital, double riskPct, double entry, double stopLoss, double leverage = 1)
    {
        if (leverage <= 0) leverage = 1;
        double slDist = Math.Abs(entry - stopLoss);
        if (capital <= 0 || riskPct <= 0 || entry <= 0 || stopLoss <= 0 || slDist <= 0)
            return new SizeResult(0, 0, 0, false);

        double slFraction = slDist / entry;            // relatief verlies per eenheid inleg
        double riskAmount = capital * riskPct / 100.0; // toegestaan verlies in USDT
        double amount     = riskAmount / (slFraction * leverage);
        return new SizeResult(Math.Round(amount, 2), Math.Round(riskAmount, 2), slFraction * 100, true);
    }

    /// <summary>
    /// Omgekeerd: welk percentage van het kapitaal staat op het spel bij dit inlegbedrag als de SL wordt geraakt?
    /// </summary>
    public static double RiskPctOfCapital(double amount, double entry, double stopLoss, double capital, double leverage = 1)
    {
        if (leverage <= 0) leverage = 1;
        double slDist = Math.Abs(entry - stopLoss);
        if (capital <= 0 || entry <= 0 || stopLoss <= 0 || amount <= 0 || slDist <= 0) return 0;

        double loss = amount * leverage * (slDist / entry);
        return loss / capital * 100.0;
    }
}
