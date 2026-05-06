using System;
using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

public class SentimentReading
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public SentimentSource Source { get; set; }
    public double SentimentScore { get; set; }
    public double Confidence { get; set; }
    public int MentionCount { get; set; }
    public DateTime Timestamp { get; set; }
    public string RawSnippet { get; set; } = string.Empty;

    public Coin Coin { get; set; } = null!;
}
