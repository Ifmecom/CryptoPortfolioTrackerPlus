using System;
using System.Collections.Generic;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure poort die bepaalt of een coin überhaupt een tradable setup mag krijgen. Voorkomt dat er
/// trade-adviezen verschijnen op coins zonder edge: stablecoins en (bijna) vlakke/zijwaartse coins.
///
/// Reden: een setup wordt gespreid op de ATR. Bij nul/zeer lage volatiliteit verzonnen de
/// detectors voorheen een ATR (`prijs × 2,5–3%`), waardoor een keurige maar betekenisloze setup op
/// een stille coin verscheen. Deze poort vervangt die kunstgreep door een expliciete drempel.
/// </summary>
public static class TradeSetupGate
{
    /// <summary>Minimale ATR als fractie van de koers om een setup te rechtvaardigen (1,5%).</summary>
    public const double MinAtrPctForSetup = 0.015;

    private static readonly HashSet<string> Stablecoins = new(StringComparer.OrdinalIgnoreCase)
    {
        "USDT", "USDC", "DAI", "TUSD", "USDP", "USDD", "FDUSD", "GUSD", "BUSD",
        "USDE", "PYUSD", "FRAX", "LUSD", "USDS", "USTC", "EURT", "EURS", "EURC", "USD0",
    };

    /// <summary>True voor bekende fiat-gekoppelde stablecoins (symbool-match, hoofdletterongevoelig).</summary>
    public static bool IsStablecoin(string? symbol)
        => !string.IsNullOrWhiteSpace(symbol) && Stablecoins.Contains(symbol.Trim());

    /// <summary>
    /// Bepaalt of een coin een tradable setup mag krijgen. Geeft <c>(false, reden)</c> terug voor
    /// stablecoins en coins waarvan de ATR onder <paramref name="minAtrPct"/> van de koers ligt;
    /// anders <c>(true, null)</c>.
    /// </summary>
    public static (bool Ok, string? Reason) Evaluate(
        string? symbol, double price, double atr, double minAtrPct = MinAtrPctForSetup)
    {
        if (IsStablecoin(symbol))
            return (false, $"Stablecoin ({symbol!.Trim().ToUpperInvariant()}) — geen trade setup.");

        double atrPct = price > 0 && atr > 0 ? atr / price : 0;
        if (atrPct < minAtrPct)
            return (false,
                $"Te lage volatiliteit (ATR {atrPct * 100:F1}% < {minAtrPct * 100:F1}% van de koers) — "
                + "stabiele/zijwaartse coin, geen tradable setup.");

        return (true, null);
    }
}
