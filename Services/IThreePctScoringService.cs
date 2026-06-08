using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Berekent de gewogen 5-factor score (Sprint A) voor één coin.
/// Werkt op OhlcvBar-lijsten (echte volume aanwezig), niet op MarketChart-JSON.
/// </summary>
public interface IThreePctScoringService
{
    /// <summary>
    /// Scoort de coin op basis van de opgegeven OHLCV-bars en richting (5-factor, Sprint A).
    /// Geeft null terug als er onvoldoende data is (minimum 210 bars).
    /// </summary>
    ThreePctScoreResult? Score(
        List<OhlcvBar>       bars,
        string               symbol,
        string               coinName,
        string               bias,
        BacktestParameters   pars);

    /// <summary>
    /// Score + Sprint B gatekeeper-check: voeg F6 (liquiditeit) en F7 (positionering) toe.
    /// De 5-factor score blijft ongewijzigd (zodat de kalibratie geldig blijft).
    /// F6 en F7 zijn gatekeepers: bij te lage scores wordt de setup als gefilterd gemarkeerd.
    /// </summary>
    ThreePctScoreResult? ScoreWithGatekeepers(
        List<OhlcvBar>       bars,
        string               symbol,
        string               coinName,
        string               bias,
        BacktestParameters   pars,
        OrderBookSnapshot?   orderBook    = null,
        FuturesPositioning?  positioning  = null);

    /// <summary>Geeft de scoreklasse ("0-40" / "41-60" / "61-80" / "81-100") voor een score.</summary>
    static string GetScoreClass(double score) => score switch
    {
        <= 40  => "0-40",
        <= 60  => "41-60",
        <= 80  => "61-80",
        _      => "81-100",
    };
}
