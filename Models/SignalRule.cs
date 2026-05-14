namespace CryptoPortfolioTracker.Models;

public class SignalRule
{
    public int Id { get; set; }
    public int? NarrativeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IndicatorConditionsJson { get; set; } = string.Empty;
    public double SentimentThreshold { get; set; }
    public double ScoreThreshold { get; set; }
    public bool IsActive { get; set; }

    public Narrative? Narrative { get; set; }
}
