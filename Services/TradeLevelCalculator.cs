namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, gedeelde rekenkern voor trade-setup niveaus (entry/SL/TP/R:R).
///
/// Deze logica was woordelijk gedupliceerd in TradeAnalysisService én
/// PatternTradingService. Hier geëxtraheerd zodat ze één bron van waarheid heeft
/// en volledig unit-testbaar is — geen UI/EF/netwerk.
/// </summary>
public static class TradeLevelCalculator
{
    /// <summary>ATR-gebaseerde ruwe niveaus voor de opgegeven richting.</summary>
    /// <param name="direction">"Long" of "Short".</param>
    public static (double stopLoss, double target1, double target2) FromAtr(
        string direction,
        double entry,
        double atr,
        double slMult = 1.5,
        double t1Mult = 2.0,
        double t2Mult = 3.5)
    {
        bool isLong = !direction.Equals("Short", StringComparison.OrdinalIgnoreCase);
        return isLong
            ? (entry - slMult * atr, entry + t1Mult * atr, entry + t2Mult * atr)
            : (entry + slMult * atr, entry - t1Mult * atr, entry - t2Mult * atr);
    }

    /// <summary>
    /// Zorgt dat Target1 altijd het DICHTSTBIJZIJNDE (eerst geraakte) doel is.
    ///   Long  → beide boven entry; Target1 &lt; Target2 (lagere prijs eerst geraakt)
    ///   Short → beide onder entry; Target1 &gt; Target2 (hogere prijs eerst geraakt)
    /// Een S/R-override kan Target2 per ongeluk dichterbij plaatsen dan Target1 — dit herstelt dat.
    /// </summary>
    public static (double target1, double target2) OrderTargets(
        string direction, double entry, double target1, double target2)
    {
        bool isLong = !direction.Equals("Short", StringComparison.OrdinalIgnoreCase);

        if (isLong && target1 > target2 && target2 > entry)
            return (target2, target1);
        if (!isLong && target1 < target2 && target2 < entry)
            return (target2, target1);

        return (target1, target2);
    }

    /// <summary>Percentages (absoluut) en reward/risk-verhoudingen.</summary>
    public static (double stopLossPct, double target1Pct, double target2Pct,
                   double riskReward1, double riskReward2) Percentages(
        double entry, double stopLoss, double target1, double target2)
    {
        if (entry <= 0)
            return (0, 0, 0, 0, 0);

        double slPct = Math.Abs((stopLoss - entry) / entry * 100);
        double t1Pct = Math.Abs((target1  - entry) / entry * 100);
        double t2Pct = Math.Abs((target2  - entry) / entry * 100);
        double rr1   = slPct > 0 ? t1Pct / slPct : 0;
        double rr2   = slPct > 0 ? t2Pct / slPct : 0;

        return (slPct, t1Pct, t2Pct, rr1, rr2);
    }
}
