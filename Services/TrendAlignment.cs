namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure helper die signaleert wanneer een trade-setup-richting tegen de overkoepelende
/// daily-trend in gaat ("counter-trend").
///
/// In Pattern Trading kan een bullish patroon (falling wedge, dubbele bodem, ascending
/// triangle, oversold-bounce, …) de richting naar <c>Long</c> kantelen terwijl de daily-trend
/// bearish is — en omgekeerd. Zo'n setup staat onder druk van de dominante trend: de kans op
/// een mislukte uitbraak is groter en het patroon heeft extra bevestiging nodig.
///
/// Bewust puur en stateless zodat het rechtstreeks getest kan worden en zowel de service
/// (onderbouwingsregel) als de UI-rij (waarschuwingschip) dezelfde definitie delen.
/// </summary>
public static class TrendAlignment
{
    /// <summary>
    /// <c>true</c> als de richting tegen de daily-trend ingaat: Long bij een bearish daily-trend,
    /// of Short bij een bullish daily-trend. Neutrale/ontbrekende waarden → <c>false</c>.
    /// </summary>
    public static bool IsCounterTrend(string? direction, string? dailyBias)
    {
        if (string.IsNullOrWhiteSpace(direction) || string.IsNullOrWhiteSpace(dailyBias))
            return false;

        return (direction == "Long"  && dailyBias == "Bearish")
            || (direction == "Short" && dailyBias == "Bullish");
    }

    /// <summary>
    /// Volledige waarschuwingszin voor de onderbouwing/tooltip, of <c>null</c> als de setup
    /// mét de trend meeloopt (of de trend neutraal/onbekend is).
    /// </summary>
    public static string? CounterTrendWarning(string? direction, string? dailyBias)
    {
        if (!IsCounterTrend(direction, dailyBias))
            return null;

        return direction == "Long"
            ? "⚠️ Counter-trend: Long tegen een bearish daily-trend — het patroon staat onder druk van de "
              + "overkoepelende trend. Hogere kans op een mislukte uitbraak; wacht op bevestiging en gebruik "
              + "een kleinere positie of strakkere stop."
            : "⚠️ Counter-trend: Short tegen een bullish daily-trend — het patroon staat onder druk van de "
              + "overkoepelende trend. Hogere kans op een mislukte uitbraak; wacht op bevestiging en gebruik "
              + "een kleinere positie of strakkere stop.";
    }

    /// <summary>Kort chiplabel voor de kaart (de volledige uitleg staat in de tooltip).</summary>
    public const string ChipLabel = "⚠ Tegen daily-trend";
}
