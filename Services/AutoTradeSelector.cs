using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoPortfolioTracker.Services;

/// <summary>Een mogelijke automatische trade uit een Pattern Trading-scan.</summary>
public sealed record AutoTradeCandidate(
    string CoinApiId,
    string Symbol,        // basismunt, bv. "SOL"
    string Direction,     // "Long" / "Short" / "Neutraal"
    int    Score,         // TradabilityScore
    double Price,         // huidige koers
    double Entry,         // instap uit het setup-advies
    double StopLoss,
    double Target1,
    bool   SetupValid);

/// <summary>Instellingen voor automatisch handelen.</summary>
public sealed record AutoTradeLimits(
    bool Enabled,
    int  MinScore,
    int  MaxPerDay,
    int  MaxOpen);

/// <summary>Besluit per kandidaat: wel/niet plaatsen en waarom (voor log en Telegram).</summary>
public sealed record AutoTradeDecision(AutoTradeCandidate Candidate, bool Place, string Reason);

/// <summary>
/// Pure keuze welke Pattern Trading-setups automatisch op Bybit Demo worden geplaatst (v1.47).
/// Strikt en voorspelbaar: alleen Long (spot), score boven de drempel, geldige SL/TP, geen bestaande
/// order op dezelfde munt, en nooit meer dan het dag- en open-posities-maximum. Hoogste score eerst.
/// </summary>
public static class AutoTradeSelector
{
    public static List<AutoTradeDecision> Select(
        IEnumerable<AutoTradeCandidate> candidates,
        AutoTradeLimits limits,
        ISet<string> symbolsWithOpenOrders,
        int placedToday,
        int openPositions)
    {
        var decisions = new List<AutoTradeDecision>();
        var list = candidates.OrderByDescending(c => c.Score).ToList();

        if (!limits.Enabled)
            return list.Select(c => new AutoTradeDecision(c, false, "automatisch handelen staat uit")).ToList();

        int room = Math.Min(
            limits.MaxPerDay > 0 ? limits.MaxPerDay - placedToday : int.MaxValue,
            limits.MaxOpen   > 0 ? limits.MaxOpen   - openPositions : int.MaxValue);

        var taken = new HashSet<string>(symbolsWithOpenOrders, StringComparer.OrdinalIgnoreCase);

        foreach (var c in list)
        {
            string? why = Reject(c, limits, taken);
            if (why is null && room <= 0)
                why = placedToday >= limits.MaxPerDay && limits.MaxPerDay > 0
                    ? $"dagmaximum bereikt ({limits.MaxPerDay})"
                    : $"maximum open posities bereikt ({limits.MaxOpen})";

            if (why is not null)
            {
                decisions.Add(new AutoTradeDecision(c, false, why));
                continue;
            }

            decisions.Add(new AutoTradeDecision(c, true, $"score {c.Score} ≥ {limits.MinScore}"));
            taken.Add(c.Symbol);
            room--;
        }
        return decisions;
    }

    private static string? Reject(AutoTradeCandidate c, AutoTradeLimits limits, ISet<string> taken)
    {
        if (c.Direction != "Long")
            return c.Direction == "Short" ? "Short kan niet op spot" : "geen richting";
        if (c.Score < limits.MinScore)
            return $"score {c.Score} < {limits.MinScore}";
        if (!c.SetupValid)
            return "setup ongeldig";
        if (c.StopLoss <= 0 || c.Target1 <= 0)
            return "geen stop-loss of take-profit";
        // Verwachte instap: de limietprijs bij een terugval-order, anders de huidige koers.
        double entryRef = LimitPriceFor(c) > 0 ? c.Entry : c.Price;
        if (entryRef <= 0)
            return "geen koers";
        if (c.StopLoss >= entryRef)
            return "stop-loss ligt niet onder de instap";
        if (c.Target1 <= entryRef * (1 + (double)BybitOrderPlanner.MarketSlippagePct / 100.0))
            return "take-profit ligt niet boven de instap";
        if (taken.Contains(c.Symbol))
            return "er loopt al een order op deze munt";
        return null;
    }

    /// <summary>
    /// Instapprijs voor een automatische order: ligt de advies-instap duidelijk onder de koers, dan een
    /// limit-order op die instap (wachten op terugval); anders direct (nagebootste market). 0 = direct.
    /// </summary>
    public static double LimitPriceFor(AutoTradeCandidate c, double pullbackThresholdPct = 0.5)
        => c.Entry > 0 && c.Price > 0 && c.Entry < c.Price * (1 - pullbackThresholdPct / 100.0)
            ? c.Entry
            : 0;
}
