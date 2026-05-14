using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;
using System.Net.Http;
using System.Text.Json;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Gate.io public candlestick API — no API key required.
///
/// Gate.io specifics:
///   • Symbol format:  "PONKE_USDT"  (underscore)
///   • Interval names: 1d / 4h / 1h / 7d (weekly)
///   • Response order: oldest-first (chronological — no reverse needed)
///   • Column order:   [timestamp_sec, quote_volume, close, high, low, open, ...]
///                      index:          0             1            2     3     4     5
/// </summary>
public class GateIoDataService : IGateIoDataService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string BaseUrl = "https://api.gateio.ws/api/v4/spot/candlesticks";

    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(GateIoDataService).PadRight(22));

    // Binance-style interval → Gate.io interval
    private static readonly Dictionary<string, string> IntervalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1w"]  = "7d",
        ["1d"]  = "1d",
        ["4h"]  = "4h",
        ["1h"]  = "1h",
        ["15m"] = "15m",
        ["5m"]  = "5m",
        ["1m"]  = "1m",
    };

    public string ResolveSymbol(string coinSymbol)
        => coinSymbol.ToUpperInvariant().Replace("-", "_").Replace("USDT", "") + "_USDT";

    public async Task<List<OhlcvBar>> GetKlinesAsync(string gateSymbol, string interval, int limit = 200)
    {
        try
        {
            if (!IntervalMap.TryGetValue(interval, out var gateInterval))
            {
                Logger.Warning("Gate.io: unknown interval '{Interval}'", interval);
                return new List<OhlcvBar>();
            }

            long to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var url = $"{BaseUrl}?currency_pair={gateSymbol}&interval={gateInterval}&limit={Math.Min(limit, 1000)}&to={to}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("Gate.io {Symbol}/{Interval} → HTTP {Code}", gateSymbol, interval, (int)response.StatusCode);
                return new List<OhlcvBar>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Gate.io returns a JSON array directly (no wrapper object)
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<OhlcvBar>();

            var bars = new List<OhlcvBar>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.GetArrayLength() < 6) continue;

                // Format: [timestamp_sec, quote_vol, close, high, low, open]
                if (!long.TryParse(row[0].GetString(), out var sec)) continue;

                bars.Add(new OhlcvBar
                {
                    Date   = DateTimeOffset.FromUnixTimeSeconds(sec).UtcDateTime,
                    Open   = ParseDouble(row[5]),
                    High   = ParseDouble(row[3]),
                    Low    = ParseDouble(row[4]),
                    Close  = ParseDouble(row[2]),
                    Volume = ParseDouble(row[1]),
                });
            }

            // Gate.io returns oldest-first (already chronological)
            Logger.Debug("Gate.io {Symbol}/{Interval}: {Count} bars", gateSymbol, interval, bars.Count);
            return bars;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch Gate.io klines {Symbol}/{Interval}", gateSymbol, interval);
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
