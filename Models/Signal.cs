using System;
using CryptoPortfolioTracker.Enums;

namespace CryptoPortfolioTracker.Models;

public class Signal
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public int? NarrativeId { get; set; }
    public Timeframe Timeframe { get; set; }
    public double TaScore { get; set; }
    public double SentimentScore { get; set; }
    public double MarketRegimeMultiplier { get; set; }
    public double CombinedScore { get; set; }
    public SignalDirection Direction { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Acknowledged { get; set; }
    public bool ActedOn { get; set; }

    public Coin Coin { get; set; } = null!;
    public Narrative? Narrative { get; set; }
}
