using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

public class BronSource
{
    public int Id { get; set; }
    public SentimentSource Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public double ReliabilityScore { get; set; }
    public bool IsActive { get; set; }
}
