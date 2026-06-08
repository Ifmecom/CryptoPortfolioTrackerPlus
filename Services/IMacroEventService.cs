using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>Levert aankomende macro-events op binnen een opgegeven datumvenster.</summary>
public interface IMacroEventService
{
    /// <summary>
    /// Geeft alle macro-events terug in de periode [<paramref name="from"/>, <paramref name="to"/>].
    /// </summary>
    IReadOnlyList<MacroEvent> GetEvents(DateTime from, DateTime to);

    /// <summary>
    /// Handig: geeft events terug in de komende <paramref name="days"/> dagen.
    /// </summary>
    IReadOnlyList<MacroEvent> GetUpcoming(int days = 15);
}
