using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure opbouw van een TradingView-watchlistbestand (v1.48). TradingView → Watchlist → ⋯ → "Lijst importeren"
/// leest een tekstbestand met tickers gescheiden door komma's; "###Naam" begint een sectie.
/// </summary>
public static class TradingViewWatchlist
{
    /// <summary>
    /// Bouwt de bestandsinhoud. Lege secties en lege/dubbele tickers worden overgeslagen (een ticker komt maar
    /// één keer voor, in de eerste sectie waarin hij staat). Sectienamen mogen geen komma bevatten.
    /// </summary>
    public static string Build(IEnumerable<(string Section, IEnumerable<string> Tickers)> sections)
    {
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();

        foreach (var (section, tickers) in sections)
        {
            var list = tickers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Where(seen.Add)
                .ToList();
            if (list.Count == 0) continue;

            if (!string.IsNullOrWhiteSpace(section))
                parts.Add("###" + section.Replace(",", " ").Trim());
            parts.AddRange(list);
        }

        return string.Join(",", parts);
    }
}
