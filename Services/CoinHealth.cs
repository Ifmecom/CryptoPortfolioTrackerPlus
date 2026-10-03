using System.Collections.Generic;

namespace CryptoPortfolioTracker.Services;

/// <summary>Uitkomst van <see cref="CoinHealth.Evaluate"/>.</summary>
/// <param name="Excluded">True = dood/onverhandelbaar of geen bruikbare data: doet niet mee in de Top X.</param>
/// <param name="Factor">Vermenigvuldiger voor de kansscore (1 = geen bezwaar).</param>
/// <param name="Reasons">Leesbare redenen (voor de tooltip of het log).</param>
public sealed record CoinHealthResult(bool Excluded, double Factor, IReadOnlyList<string> Reasons)
{
    public static readonly CoinHealthResult Healthy = new(false, 1.0, System.Array.Empty<string>());
}

/// <summary>
/// Pure check of een munt gezond genoeg is om als top-kans te tonen (v1.48). Aanleiding: de signaalengine
/// beloont extremen (overbought/oversold, buiten de Bollinger-banden), en bij dode of instortende munten zijn
/// die extremen een bijwerking van stilstaande of kapotte koersdata (ATR 0, RSI precies 100) of een vallend
/// mes — geen kans. Praktijkvoorbeelden: WMOXY (marktwaarde 0, rang onbekend, ATR 0), NEIRO (−93% in een
/// maand, ATR 0), NIBI ($0,5 mln marktwaarde, 84% onder MA50).
/// </summary>
public static class CoinHealth
{
    /// <summary>Rang die munten zonder CoinGecko-rang in de app krijgen.</summary>
    public const long UnknownRank = 999999;

    /// <summary>Onder deze marktwaarde (USD) nauwelijks verhandelbaar → doet niet mee.</summary>
    public const double MinMarketCapUsd = 1_000_000;

    /// <summary>Onder deze marktwaarde (USD) telt een munt als microcap.</summary>
    public const double MicroCapUsd = 10_000_000;

    /// <summary>Daling over een maand (%) waaronder een munt als ingestort telt.</summary>
    public const double CollapseMonthPct = -70;

    /// <summary>Afstand tot het 50-daags gemiddelde (%) waaronder een munt als ingestort telt.</summary>
    public const double CollapseBelowMa50Pct = -50;

    public const double MicroCapFactor   = 0.80;
    public const double CollapsingFactor = 0.60;

    /// <summary>
    /// Beoordeelt een munt. <paramref name="minAtrFraction"/> is dezelfde volatiliteitsdrempel als de setup-poort
    /// (<see cref="TradeSetupGate"/>; Settings.MinSetupAtrPercent / 100). ATR 0 = geen beweging of geen data.
    /// Onbekende waarden: <paramref name="atr"/> of <paramref name="ma50DistPct"/> = null slaat die check over.
    /// </summary>
    public static CoinHealthResult Evaluate(
        double price, double marketCap, long rank, double change1MonthPct,
        double? atr, double? ma50DistPct, double minAtrFraction)
    {
        if (price <= 0)
            return Exclude("geen koers");
        if (marketCap <= 0 || rank <= 0 || rank >= UnknownRank)
            return Exclude("geen marktwaarde of onbekende rang — wordt niet of nauwelijks verhandeld");
        if (marketCap < MinMarketCapUsd)
            return Exclude($"marktwaarde ${marketCap / 1_000_000:0.00} mln — nauwelijks verhandelbaar");
        if (atr is { } a)
        {
            if (a <= 0)
                return Exclude("geen beweging (ATR 0) — stilstaande of ontbrekende koersdata");
            if (a / price < minAtrFraction)
                return Exclude($"te weinig beweging (ATR {a / price * 100:0.0}% < {minAtrFraction * 100:0.0}% van de koers)");
        }

        double factor  = 1.0;
        var    reasons = new List<string>();

        bool monthCrash = change1MonthPct <= CollapseMonthPct;
        bool belowMa50  = ma50DistPct is { } d && d <= CollapseBelowMa50Pct;
        if (monthCrash || belowMa50)
        {
            factor *= CollapsingFactor;
            reasons.Add(monthCrash
                ? $"Ingestort ({change1MonthPct:0}% in een maand) → ×{CollapsingFactor:0.00}"
                : $"Ingestort ({ma50DistPct:0}% onder het 50-daags gemiddelde) → ×{CollapsingFactor:0.00}");
        }
        if (marketCap < MicroCapUsd)
        {
            factor *= MicroCapFactor;
            reasons.Add($"Microcap (marktwaarde ${marketCap / 1_000_000:0.0} mln) → ×{MicroCapFactor:0.00}");
        }

        return reasons.Count == 0 ? CoinHealthResult.Healthy : new CoinHealthResult(false, factor, reasons);
    }

    private static CoinHealthResult Exclude(string reason) => new(true, 0, new[] { reason });
}
