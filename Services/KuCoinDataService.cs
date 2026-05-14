using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;
using System.Net.Http;
using System.Text.Json;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Fetches OHLCV candlestick data from the KuCoin public REST API.
/// No API key required. Used as fallback when a coin is not listed on Binance.
///
/// KuCoin specifics vs. Binance:
///   • Symbol format:  "PONKE-USDT"  (hyphen-separated)
///   • Interval names: 1week / 1day / 4hour / 1hour
///   • Response order: newest-first  (must be reversed)
///   • Column order:   [timestamp, open, CLOSE, HIGH, LOW, volume, turnover]
///                      (close=2, high=3, low=4 — different from Binance!)
/// </summary>
public class KuCoinDataService : IKuCoinDataService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string BaseUrl = "https://api.kucoin.com/api/v1/market/candles";

    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(KuCoinDataService).PadRight(22));

    // Binance-style interval → KuCoin interval name
    private static readonly Dictionary<string, string> IntervalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1w"] = "1week",
        ["1d"] = "1day",
        ["4h"] = "4hour",
        ["1h"] = "1hour",
        ["15m"] = "15min",
        ["5m"]  = "5min",
        ["1m"]  = "1min",
    };

    // Interval → duration in seconds (used to compute startAt)
    private static readonly Dictionary<string, long> IntervalSeconds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1w"] = 7 * 24 * 3600,
        ["1d"] = 24 * 3600,
        ["4h"] = 4  * 3600,
        ["1h"] = 3600,
        ["15m"] = 15 * 60,
        ["5m"]  = 5  * 60,
        ["1m"]  = 60,
    };

    public string ResolveKuCoinSymbol(string coinSymbol)
        => coinSymbol.ToUpperInvariant().TrimEnd('T', 'U', 'S', 'D') // strip USDT suffix if already appended
                                        .Trim('-')
           is var stripped && stripped.EndsWith("USDT")
            ? stripped[..^4] + "-USDT"
            : coinSymbol.ToUpperInvariant() + "-USDT";

    public async Task<List<OhlcvBar>> GetKlinesAsync(string kuCoinSymbol, string interval, int limit = 200)
    {
        try
        {
            if (!IntervalMap.TryGetValue(interval, out var kuInterval))
            {
                Logger.Warning("KuCoin: unknown interval '{Interval}'", interval);
                return new List<OhlcvBar>();
            }

            // KuCoin requires startAt / endAt in Unix seconds
            long endAt   = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long secPer  = IntervalSeconds.GetValueOrDefault(interval, 86400);
            long startAt = endAt - (limit + 10) * secPer;   // +10 buffer for weekend/holiday gaps

            var url = $"{BaseUrl}?type={kuInterval}&symbol={kuCoinSymbol}&startAt={startAt}&endAt={endAt}";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("KuCoin {Symbol}/{Interval} → HTTP {Code}", kuCoinSymbol, interval, (int)response.StatusCode);
                return new List<OhlcvBar>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // KuCoin wraps in { "code": "200000", "data": [...] }
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                Logger.Debug("KuCoin {Symbol}: no 'data' in response", kuCoinSymbol);
                return new List<OhlcvBar>();
            }

            var bars = new List<OhlcvBar>();
            foreach (var row in data.EnumerateArray())
            {
                // Format: [timestamp_sec, open, close, high, low, volume, turnover]
                if (row.GetArrayLength() < 6) continue;

                var ts = row[0].GetString();
                if (!long.TryParse(ts, out var sec)) continue;

                bars.Add(new OhlcvBar
                {
                    Date   = DateTimeOffset.FromUnixTimeSeconds(sec).UtcDateTime,
                    Open   = ParseDouble(row[1]),
                    High   = ParseDouble(row[3]),  // KuCoin: index 3 = high
                    Low    = ParseDouble(row[4]),  // KuCoin: index 4 = low
                    Close  = ParseDouble(row[2]),  // KuCoin: index 2 = close
                    Volume = ParseDouble(row[5]),
                });
            }

            // KuCoin returns newest-first — reverse to chronological order
            bars.Reverse();

            // Take the most recent `limit` bars
            if (bars.Count > limit)
                bars = bars.TakeLast(limit).ToList();

            Logger.Debug("KuCoin {Symbol}/{Interval}: {Count} bars", kuCoinSymbol, interval, bars.Count);
            return bars;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch KuCoin klines {Symbol}/{Interval}", kuCoinSymbol, interval);
            return new List<OhlcvBar>();
        }
    }

    private static double ParseDouble(JsonElement el)
    {
        var s = el.GetString();
        return s is not null && double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
