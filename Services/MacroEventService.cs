using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Eenvoudige macro-event kalender voor 3%-trading-risicobeheer.
///
/// Vaste datums:
///   FOMC 2025/2026 — hardcoded Fed-vergaderdagen (beslissingsdag = tweede dag)
///   US CPI         — algoritmisch: 2e dinsdag van de maand ± 2 dagen (benadering)
///   US NFP         — algoritmisch: eerste vrijdag van de maand
///   US PCE         — algoritmisch: laatste vrijdag van de maand
///
/// BELANGRIJK: CPI en NFP zijn benaderingen. Controleer altijd de exacte datum
/// bij de officiële bronnen (BLS, Fed).
/// </summary>
public class MacroEventService : IMacroEventService
{
    // ── FOMC-beslissingsdata (dag 2 van de vergadering) ──────────────────────
    private static readonly DateTime[] FomcDates =
    {
        // 2025
        new(2025,  1, 29), new(2025,  3, 19), new(2025,  5,  7),
        new(2025,  6, 18), new(2025,  7, 30), new(2025,  9, 17),
        new(2025, 10, 29), new(2025, 12, 10),
        // 2026
        new(2026,  1, 28), new(2026,  3, 18), new(2026,  4, 29),
        new(2026,  6, 10), new(2026,  7, 29), new(2026,  9, 16),
        new(2026, 11,  4), new(2026, 12,  9),
    };

    // =========================================================================
    // Interface implementatie
    // =========================================================================

    /// <summary>
    /// Bouwt de kalender voor exact het opgevraagde venster, zodat elke
    /// datumrange werkt (niet gebonden aan een vast opstartvenster).
    /// </summary>
    public IReadOnlyList<MacroEvent> GetEvents(DateTime from, DateTime to)
        => BuildCalendar(from.Date, to.Date)
               .Where(e => e.Date >= from.Date && e.Date <= to.Date)
               .OrderBy(e => e.Date)
               .ToList();

    public IReadOnlyList<MacroEvent> GetUpcoming(int days = 15)
        => GetEvents(DateTime.Today, DateTime.Today.AddDays(days));

    // =========================================================================
    // Kalenderbouw
    // =========================================================================

    private static List<MacroEvent> BuildCalendar(DateTime from, DateTime to)
    {
        var events = new List<MacroEvent>();

        // ── FOMC ─────────────────────────────────────────────────────────────
        foreach (var d in FomcDates)
            if (d >= from && d <= to)
                events.Add(new MacroEvent("FOMC", d,
                    "Federal Reserve rentebeslissing — hoge volatiliteit verwacht"));

        // ── Per maand: NFP, CPI, PCE ─────────────────────────────────────────
        for (var m = new DateTime(from.Year, from.Month, 1);
             m <= to;
             m = m.AddMonths(1))
        {
            // NFP: eerste vrijdag van de maand
            var nfp = FirstWeekdayOfMonth(m, DayOfWeek.Friday);
            if (nfp >= from && nfp <= to)
                events.Add(new MacroEvent("US NFP", nfp,
                    "Non-Farm Payrolls (arbeidsmarkt) — hoge volatiliteit USD/risico"));

            // CPI: tweede dinsdag ± 2 dagen (benadering — check officieel)
            var cpi = SecondWeekdayOfMonth(m, DayOfWeek.Wednesday);
            if (cpi >= from && cpi <= to)
                events.Add(new MacroEvent("US CPI", cpi,
                    "Consumer Price Index (inflatie) — BENADERING, check BLS.gov"));

            // PCE: laatste vrijdag van de maand (Personal Consumption Expenditure)
            var pce = LastWeekdayOfMonth(m, DayOfWeek.Friday);
            if (pce >= from && pce <= to)
                events.Add(new MacroEvent("US PCE", pce,
                    "PCE-inflatie (Fed-voorkeurmeter) — BENADERING"));
        }

        return events.OrderBy(e => e.Date).ToList();
    }

    // ── Datum-hulpmethoden ────────────────────────────────────────────────────

    private static DateTime FirstWeekdayOfMonth(DateTime month, DayOfWeek day)
    {
        var d = new DateTime(month.Year, month.Month, 1);
        while (d.DayOfWeek != day) d = d.AddDays(1);
        return d;
    }

    private static DateTime SecondWeekdayOfMonth(DateTime month, DayOfWeek day)
    {
        var first = FirstWeekdayOfMonth(month, day);
        return first.AddDays(7);
    }

    private static DateTime LastWeekdayOfMonth(DateTime month, DayOfWeek day)
    {
        int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var d = new DateTime(month.Year, month.Month, daysInMonth);
        while (d.DayOfWeek != day) d = d.AddDays(-1);
        return d;
    }
}
