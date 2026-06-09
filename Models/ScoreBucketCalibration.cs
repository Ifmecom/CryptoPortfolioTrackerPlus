namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Werkelijk behaalde prestaties per scoreklasse, afgeleid uit gesloten WatchedSetups
/// (de feedback-loop: niet een backtest-aanname maar wat in de praktijk uitkwam).
/// </summary>
public record ScoreBucketCalibration(
    string ScoreClass,
    int    TradeCount,
    int    Won,
    int    Lost,
    double WinRatePct,
    double Expectancy,   // gemiddelde R-multiple over de gesloten setups in deze klasse
    bool   IsReliable)   // genoeg trades voor een betekenisvolle uitspraak
{
    public string WinRateDisplay   => TradeCount > 0 ? $"{WinRatePct:0.0}%" : "—";
    public string ExpectancyDisplay => TradeCount > 0 ? $"{Expectancy:+0.00;-0.00}R" : "—";
    public string CountDisplay      => $"{TradeCount} ({Won}W / {Lost}L)";
}
