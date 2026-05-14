using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;
using System.Net.Http;
using System.Text.Json;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// MEXC public klines API — no API key required.
/// MEXC uses a Binance-compatible endpoint, with minor interval naming differences.
///
/// MEXC specifics:
///   • Symbol format:  "PONKEUSDT"  (same as Binance, no separator)
///   • Interval names: 1m / 5m / 15m / 30m / 60m (1h) / 4h / 1d / 1W (weekly)
///   • Response order: oldest-first (same as Binance)
///   • Column order:   [openTime, open, high, low, close, volume, closeTime, ...]
///                      same as Binance
/// </summary>
public class MexcDataService : IMexcDataService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string BaseUrl = "https://api.mexc.com/api/v3/klines";

    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(MexcDataService).PadRight(22));

    // Binance-style interval → MEXC interval (capital W for weekly, 60m for 1h)
    private static readonly Dictionary<string, string> IntervalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1w"]  = "1W",
        ["1d"]  = "1d",
        ["4h"]  = "4h",
        ["1h"]  = "60m",
        ["15m"] = "15m",
        ["5m"]  = "5m",
        ["1m"]  = "1m",
    };

    public string ResolveSymbol(string coinSymbol)
        => coinSymbol.ToUpperInvariant().TrimEnd('T','U','S','D').TrimEnd('-') is var s
           && s.EndsWith("USDT") ? s : coinSymbol.ToUpperInvariant() + "USDT";

    public async Task<List<OhlcvBar>> GetKlinesAsync(string mexcSymbol, string interval, int limit = 200)
    {
        try
        {
            if (!IntervalMap.TryGetValue(interval, out var mexcInterval))
            {
                Logger.Warning("MEXC: unknown interval '{Interval}'", interval);
                return new List<OhlcvBar>();
            }

            var url = $"{BaseUrl}?symbol={mexcSymbol}&interval={mexcInterval}&limit={Math.Min(limit, 1000)}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("MEXC {Symbol}/{Interval} → HTTP {Code}", mexcSymbol, interval, (int)response.StatusCode);
                return new List<OhlcvBar>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<OhlcvBar>();

            var bars = new List<OhlcvBar>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.GetArrayLength() < 6) continue;

                // Binance-compatible format: [openTime, open, high, low, close, volume, ...]
                bars.Add(new OhlcvBar
                {
                    Date   = DateTimeOffset.FromUnixTimeMilliseconds(row[0].GetInt64()).UtcDateTime,
                    Open   = ParseDouble(row[1]),
                    High   = ParseDouble(row[2]),
                    Low    = ParseDouble(row[3]),
                    Close  = ParseDouble(row[4]),
                    Volume = ParseDouble(row[5]),
                });
            }

            Logger.Debug("MEXC {Symbol}/{Interval}: {Count} bars", mexcSymbol, interval, bars.Count);
            return bars;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch MEXC klines {Symbol}/{Interval}", mexcSymbol, interval);
            return new List<OhlcvBar>();
        }
    }

    private static double ParseDouble(JsonElement el)
    {
        // MEXC returns numbers as strings in some versions, raw numbers in others
        if (el.ValueKind == JsonValueKind.Number)
            return el.GetDouble();
        var s = el.GetString();
        return s is not null && double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
