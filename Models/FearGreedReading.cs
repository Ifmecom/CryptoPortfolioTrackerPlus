using System;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Represents one snapshot of the Crypto Fear &amp; Greed Index
/// (source: https://api.alternative.me/fng/).
/// </summary>
public class FearGreedReading
{
    public int      Id             { get; set; }

    /// <summary>0 = Extreme Fear · 100 = Extreme Greed</summary>
    public int      Value          { get; set; }

    /// <summary>"Extreme Fear" | "Fear" | "Neutral" | "Greed" | "Extreme Greed"</summary>
    public string   Classification { get; set; } = string.Empty;

    public DateTime Timestamp      { get; set; }
}
