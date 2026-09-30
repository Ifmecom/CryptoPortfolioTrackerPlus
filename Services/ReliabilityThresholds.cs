namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Eén centrale plek voor de minimale steekproefgroottes waarboven een gemeten win-rate/uitkomst als
/// "betrouwbaar" wordt getoond. Voorheen stonden deze als losse magic numbers verspreid (10 in
/// <see cref="SetupOutcomeCalibrator"/>, 30 in <see cref="ThreePctBacktestService"/>); ze zijn hier
/// gebundeld met een expliciete reden zodat de verschillende feedback-overzichten consistent en
/// uitlegbaar zijn (item 9).
/// </summary>
public static class ReliabilityThresholds
{
    /// <summary>
    /// Min. aantal beslissende uitkomsten (PlayedOut + Invalidated) per patroontype voordat de
    /// hit-rate als betrouwbaar geldt — fijnmazig (per type), dus een lagere drempel volstaat.
    /// </summary>
    public const int MinDecisive = 10;

    /// <summary>
    /// Min. aantal gesloten setups per scoreklasse voor de expectancy-kalibratie — eveneens fijnmazig
    /// (per klasse), zelfde ordegrootte als <see cref="MinDecisive"/>.
    /// </summary>
    public const int MinClosedSetups = 10;

    /// <summary>
    /// Min. aantal trades in een 3%-backtest-bucket. Hoger, want dit is een grovere, strategie-brede
    /// meting waarbij statistische ruis pas bij meer samples uitmiddelt.
    /// </summary>
    public const int MinBacktestTrades = 30;

    /// <summary>
    /// Min. aantal gemeten signaal-uitkomsten per groep (bron + richting + scoreklasse) in de
    /// signaal-kalibratie (v1.46). Hoger dan <see cref="MinDecisive"/>: een rendement na N dagen is veel
    /// ruiziger dan een uitgespeeld/mislukt patroon, en signalen op dezelfde dag bewegen vaak samen
    /// mee met de markt (onderling afhankelijk), dus er zijn meer metingen nodig voordat de trefkans iets zegt.
    /// </summary>
    public const int MinSignalOutcomes = 20;
}
