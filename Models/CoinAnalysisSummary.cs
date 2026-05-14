namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Compact projection of a TradeAnalysisResult used in the "Analyseer alles" ranked list.
/// One row per coin, sorted by signal strength.
/// </summary>
public class CoinAnalysisSummary
{
    public Coin   Coin         { get; init; } = null!;
    public int    Score        { get; init; }
    public string Direction    { get; init; } = string.Empty;   // Long / Short / Geen signaal
    public string Confidence   { get; init; } = string.Empty;   // Laag / Gemiddeld / Hoog
    public string DailyBias    { get; init; } = string.Empty;   // Bullish / Bearish / Neutraal
    public string WeeklyBias   { get; init; } = string.Empty;
    public double EntryPrice   { get; init; }
    public double StopLoss     { get; init; }
    public double StopLossPct  { get; init; }
    public double Target1      { get; init; }
    public double Target1Pct   { get; init; }
    public double RiskReward1  { get; init; }
    public string DataSource   { get; init; } = string.Empty;
    public bool   HasLiveData  { get; init; }  // false = only local cache

    public CoinAnalysisSummary(Coin coin, TradeAnalysisResult result)
    {
        Coin        = coin;
        Score       = result.CombinedScore;
        Direction   = result.Setup.Direction;
        Confidence  = result.Setup.Confidence;
        DailyBias   = result.Daily.TrendBias;
        WeeklyBias  = result.Weekly.TrendBias;
        EntryPrice  = result.Setup.EntryPrice;
        StopLoss    = result.Setup.StopLoss;
        StopLossPct = result.Setup.StopLossPct;
        Target1     = result.Setup.Target1;
        Target1Pct  = result.Setup.Target1Pct;
        RiskReward1 = result.Setup.RiskReward1;
        DataSource  = result.DataSource;
        HasLiveData = !result.DataSource.Contains("lokale cache", StringComparison.OrdinalIgnoreCase)
                   && !result.DataSource.Contains("geen data",    StringComparison.OrdinalIgnoreCase);
    }
}
