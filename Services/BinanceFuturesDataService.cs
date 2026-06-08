using System.Text.Json;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Binance Futures API-client voor positioneringsdata.
/// Endpoints (allemaal openbaar, geen API-key vereist):
///   • /fapi/v1/fundingRate        — actuele funding rate
///   • /fapi/v1/openInterest       — open interest in basismunt
///   • /futures/data/globalLongShortAccountRatio — long/short account-ratio
///
/// In-memory cache (TTL 5 minuten) voorkomt rate-limit bij live scan van 30+ coins.
/// Spot-only coins (404 op fapi) retourneren IsAvailable = false.
/// </summary>
public class BinanceFuturesDataService : IBinanceFuturesDataService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(BinanceFuturesDataService).PadRight(22));

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private const string FapiBase = "https://fapi.binance.com";

    private readonly TtlCache<FuturesPositioning> _cache = new(TimeSpan.FromMinutes(5));

    public async Task<FuturesPositioning> GetPositioningAsync(
        string binanceSymbol, CancellationToken ct = default)
    {
        var sym = binanceSymbol.ToUpperInvariant();

        if (_cache.TryGet(sym, out var cached))
            return cached;

        try
        {
            // ── Funding rate ──────────────────────────────────────────────────
            double fundingRate = await GetFundingRateAsync(sym, ct);
            if (fundingRate == double.MinValue)
            {
                // Symbol not on futures market
                return new FuturesPositioning(sym, 0, 0, 1.0, IsAvailable: false);
            }

            // ── Open Interest ─────────────────────────────────────────────────
            double openInterest = await GetOpenInterestAsync(sym, ct);

            // ── Long/Short ratio ──────────────────────────────────────────────
            double lsRatio = await GetLongShortRatioAsync(sym, ct);

            var result = new FuturesPositioning(sym, fundingRate, openInterest, lsRatio, IsAvailable: true);

            _cache.Set(sym, result);

            Logger.Debug(
                "Futures {Symbol}: funding={FR:+0.0000;-0.0000}%, OI={OI:N0}, L/S={LS:F2}",
                sym, fundingRate, openInterest, lsRatio);

            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Warning(ex, "BinanceFutures: failed for {Symbol}", sym);
            return new FuturesPositioning(sym, 0, 0, 1.0, IsAvailable: false);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Returns double.MinValue when symbol is not on futures.</summary>
    private async Task<double> GetFundingRateAsync(string sym, CancellationToken ct)
    {
        var url = $"{FapiBase}/fapi/v1/fundingRate?symbol={sym}&limit=1";
        var resp = await _http.GetAsync(url, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return double.MinValue;   // spot-only

        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var arr = doc.RootElement;
        if (arr.GetArrayLength() == 0) return 0;

        var last = arr[arr.GetArrayLength() - 1];
        return double.Parse(
            last.GetProperty("fundingRate").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture) * 100.0; // als %
    }

    private async Task<double> GetOpenInterestAsync(string sym, CancellationToken ct)
    {
        try
        {
            var url  = $"{FapiBase}/fapi/v1/openInterest?symbol={sym}";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return 0;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return double.Parse(
                doc.RootElement.GetProperty("openInterest").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { return 0; }
    }

    private async Task<double> GetLongShortRatioAsync(string sym, CancellationToken ct)
    {
        try
        {
            var url  = $"{FapiBase}/futures/data/globalLongShortAccountRatio?symbol={sym}&period=1h&limit=1";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return 1.0;  // default neutral

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.GetArrayLength() == 0) return 1.0;

            return double.Parse(
                arr[0].GetProperty("longShortRatio").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { return 1.0; }
    }
}
