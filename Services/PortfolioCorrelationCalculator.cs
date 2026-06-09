using System;
using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare aggregatie van per-coin BTC-correlaties naar een portfolio-oordeel.
/// De per-coin Pearson-correlatie komt van <see cref="ICorrelationService"/>; deze klasse
/// weegt ze op portfoliowaarde en classificeert de diversificatie.
/// </summary>
public static class PortfolioCorrelationCalculator
{
    public const double HighThreshold   = 0.80;
    public const double MediumThreshold = 0.50;

    /// <summary>Classificeert een correlatiewaarde. NaN → "—".</summary>
    public static string Label(double correlation)
    {
        if (double.IsNaN(correlation)) return "—";
        double c = Math.Abs(correlation);
        if (c >= HighThreshold)   return "Hoog";
        if (c >= MediumThreshold) return "Gemiddeld";
        return "Laag";
    }

    /// <summary>Samenvattend diversificatie-oordeel op basis van de gewogen gemiddelde correlatie.</summary>
    public static string Verdict(double weightedAvg)
    {
        if (double.IsNaN(weightedAvg)) return "Onvoldoende data voor een oordeel.";
        double c = Math.Abs(weightedAvg);
        if (c >= HighThreshold)   return "Sterk geconcentreerd — je portfolio beweegt grotendeels met BTC mee.";
        if (c >= MediumThreshold) return "Matig gediversifieerd — een flinke BTC-afhankelijkheid.";
        return "Goed gediversifieerd — relatief losgekoppeld van BTC.";
    }

    /// <summary>
    /// Bouwt het volledige resultaat uit de per-coin correlaties (alleen geldige, value &gt; 0
    /// tellen mee in het gewogen gemiddelde). Coins worden gesorteerd op correlatie (hoog → laag).
    /// </summary>
    public static PortfolioCorrelationResult Summarize(IReadOnlyList<CoinCorrelation> coins, int totalCount)
    {
        var valid = coins.Where(c => !double.IsNaN(c.Correlation)).ToList();

        double weightedAvg = double.NaN;
        double totalValue = valid.Where(c => c.Value > 0).Sum(c => c.Value);
        if (totalValue > 0)
            weightedAvg = valid.Where(c => c.Value > 0).Sum(c => c.Correlation * c.Value) / totalValue;
        else if (valid.Count > 0)
            weightedAvg = valid.Average(c => c.Correlation); // val terug op ongewogen gemiddelde

        var ordered = coins
            .OrderByDescending(c => double.IsNaN(c.Correlation) ? double.MinValue : Math.Abs(c.Correlation))
            .ThenByDescending(c => c.Value)
            .ToList();

        return new PortfolioCorrelationResult
        {
            Coins                  = ordered,
            WeightedAvgCorrelation = weightedAvg,
            HighCount              = valid.Count(c => c.Label == "Hoog"),
            MediumCount            = valid.Count(c => c.Label == "Gemiddeld"),
            LowCount               = valid.Count(c => c.Label == "Laag"),
            Verdict                = Verdict(weightedAvg),
            AnalyzedCount          = valid.Count,
            TotalCount             = totalCount,
        };
    }
}
