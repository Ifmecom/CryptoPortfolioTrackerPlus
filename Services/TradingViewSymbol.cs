using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure omzetting naar TradingView-tickers en grafiek-links (v1.48). Geen netwerk: TradingView heeft geen
/// openbare API; de koppeling loopt via deeplinks, Pine Script en watchlist-bestanden.
/// </summary>
public static class TradingViewSymbol
{
    /// <summary>Beursnaam zoals de app hem in DataSource zet → TradingView-beursprefix.</summary>
    private static readonly Dictionary<string, string> Prefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Binance"] = "BINANCE",
        ["KuCoin"]  = "KUCOIN",
        ["Gate.io"] = "GATEIO",
        ["Gate"]    = "GATEIO",
        ["MEXC"]    = "MEXC",
        ["Bybit"]   = "BYBIT",
        ["OKX"]     = "OKX",
    };

    /// <summary>Beurzen die in Instellingen als standaard gekozen kunnen worden (prefix).</summary>
    public static readonly IReadOnlyList<string> SupportedExchanges = new[] { "BINANCE", "BYBIT", "KUCOIN", "GATEIO", "MEXC", "OKX" };

    private static readonly Regex DataSourcePattern = new(@"^\s*(?<ex>[A-Za-z.]+)\s*\((?<pair>[^)]+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Ticker voor TradingView. Uit een DataSource als "Binance (LUNCUSDT)" of "KuCoin (BTC-USDT)" komt
    /// "BINANCE:LUNCUSDT" / "KUCOIN:BTCUSDT"; anders <paramref name="defaultExchange"/>:SYMBOOL+<paramref name="quote"/>.
    /// Leeg symbool → lege string.
    /// </summary>
    public static string For(string? baseSymbol, string? dataSource = null, string defaultExchange = "BINANCE", string quote = "USDT")
    {
        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            var m = DataSourcePattern.Match(dataSource);
            if (m.Success && Prefix.TryGetValue(m.Groups["ex"].Value, out var px))
            {
                var pair = CleanPair(m.Groups["pair"].Value);
                if (pair.Length > 0) return $"{px}:{pair}";
            }
        }

        var sym = CleanSymbol(baseSymbol);
        if (sym.Length == 0) return string.Empty;
        var ex = string.IsNullOrWhiteSpace(defaultExchange) ? "BINANCE" : defaultExchange.Trim().ToUpperInvariant();
        var q  = string.IsNullOrWhiteSpace(quote) ? "USDT" : quote.Trim().ToUpperInvariant();
        return sym.EndsWith(q, StringComparison.Ordinal) && sym.Length > q.Length ? $"{ex}:{sym}" : $"{ex}:{sym}{q}";
    }

    /// <summary>Het paar zonder beursprefix ("BINANCE:LUNCUSDT" → "LUNCUSDT") — wat Pine als syminfo.ticker ziet.</summary>
    public static string PairOf(string ticker)
    {
        if (string.IsNullOrEmpty(ticker)) return string.Empty;
        int i = ticker.IndexOf(':');
        return i >= 0 ? ticker[(i + 1)..] : ticker;
    }

    /// <summary>Deeplink naar de TradingView-grafiek, bijv. https://www.tradingview.com/chart/?symbol=BINANCE%3ALUNCUSDT&amp;interval=D.</summary>
    public static string ChartUrl(string ticker, string interval = "D")
        => $"https://www.tradingview.com/chart/?symbol={Uri.EscapeDataString(ticker)}&interval={Uri.EscapeDataString(Interval(interval))}";

    /// <summary>App-timeframe ("1D", "4H", "1H", "15M", "1W") → TradingView-interval ("D", "240", "60", "15", "W").</summary>
    public static string Interval(string? timeframe) => (timeframe ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "1W" or "W" or "WEEK"  => "W",
        "4H" or "240"          => "240",
        "1H" or "60"           => "60",
        "15M" or "15"          => "15",
        _                      => "D",
    };

    private static string CleanPair(string pair)
        => Regex.Replace(pair.Trim().ToUpperInvariant(), "[^A-Z0-9]", string.Empty);

    private static string CleanSymbol(string? symbol)
        => Regex.Replace((symbol ?? string.Empty).Trim().TrimStart('$').ToUpperInvariant(), "[^A-Z0-9]", string.Empty);
}
