using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

/// <summary>Correlatie van één holding met BTC, plus het portfoliogewicht.</summary>
public record CoinCorrelation(
    string Symbol,
    string Name,
    string ImageUri,
    double Correlation,   // Pearson met BTC dagrendementen (−1..1); NaN = onvoldoende data
    double Value,         // portfoliowaarde in USD (weegt mee in het gemiddelde)
    string Label)         // "Hoog" / "Gemiddeld" / "Laag" / "—"
{
    public string CorrelationDisplay => double.IsNaN(Correlation) ? "—" : $"{Correlation:+0.00;-0.00}";
    public string ValueDisplay => Value >= 1_000_000 ? $"${Value / 1_000_000:0.##}M"
                                 : Value >= 1_000     ? $"${Value / 1_000:0.##}K"
                                 : $"${Value:0}";
}

/// <summary>Resultaat van een portfolio-correlatieanalyse t.o.v. BTC.</summary>
public class PortfolioCorrelationResult
{
    public List<CoinCorrelation> Coins { get; init; } = new();

    /// <summary>Waarde-gewogen gemiddelde correlatie van het portfolio met BTC.</summary>
    public double WeightedAvgCorrelation { get; init; }

    public int HighCount   { get; init; }
    public int MediumCount { get; init; }
    public int LowCount    { get; init; }

    /// <summary>Samenvattend oordeel over de diversificatie.</summary>
    public string Verdict { get; init; } = string.Empty;

    public int AnalyzedCount { get; init; }
    public int TotalCount    { get; init; }
}
