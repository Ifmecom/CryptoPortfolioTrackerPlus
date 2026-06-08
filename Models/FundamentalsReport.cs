using System.Collections.Generic;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Afgeleid analyse-rapport (SWOT + risico + waardering) voor één coin, berekend uit
/// <see cref="CoinFundamentals"/>. Volledig rule-based en transparant — geen externe AI.
/// </summary>
public class FundamentalsReport
{
    public string ExecutiveSummary { get; set; } = string.Empty;

    public List<string> Strengths     { get; } = new();
    public List<string> Weaknesses    { get; } = new();
    public List<string> Opportunities { get; } = new();
    public List<string> Threats       { get; } = new();
    public List<string> TopRisks      { get; } = new();

    /// <summary>LOW / MEDIUM / HIGH.</summary>
    public string RiskLevel { get; set; } = "MEDIUM";

    /// <summary>Heuristische waarderingsconclusie (geen koersdoel).</summary>
    public string ValuationVerdict { get; set; } = string.Empty;
}
