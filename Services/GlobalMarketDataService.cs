using System.Text.Json;
using CryptoPortfolioTracker.Configuration;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt globale marktdata op via CoinGecko GET /global.
/// Gecached voor 5 minuten om rate-limits te vermijden.
/// </summary>
public class GlobalMarketDataService : IGlobalMarketDataService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(GlobalMarketDataService).PadRight(22));

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "CryptoPortfolioTrackerPlus/1.0" } },
    };

    private const int CacheTtlSec = 300;
    private GlobalMarketData? _cached;
    private DateTime          _cacheExpiry = DateTime.MinValue;

    public async Task<GlobalMarketData?> GetGlobalDataAsync(CancellationToken ct = default)
    {
        if (_cached is not null && DateTime.UtcNow < _cacheExpiry)
            return _cached;

        try
        {
            var url  = AppConstants.ApiPath.TrimEnd('/') + "/global";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var data = doc.RootElement.GetProperty("data");

            // BTC dominance
            double btcDom = 0;
            if (data.TryGetProperty("market_cap_percentage", out var pct)
                && pct.TryGetProperty("btc", out var btcPct))
                btcDom = btcPct.GetDouble();

            // Total market cap (USD)
            double totalCap = 0;
            if (data.TryGetProperty("total_market_cap", out var capObj)
                && capObj.TryGetProperty("usd", out var capUsd))
                totalCap = capUsd.GetDouble();

            // Total volume (USD)
            double totalVol = 0;
            if (data.TryGetProperty("total_volume", out var volObj)
                && volObj.TryGetProperty("usd", out var volUsd))
                totalVol = volUsd.GetDouble();

            _cached      = new GlobalMarketData(btcDom, totalCap, totalVol);
            _cacheExpiry = DateTime.UtcNow.AddSeconds(CacheTtlSec);

            Logger.Debug("GlobalMarket: BTC dom={Dom:0.0}%, cap={Cap:N0}$", btcDom, totalCap);
            return _cached;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Warning(ex, "GlobalMarketDataService: fetch failed");
            return null;
        }
    }
}
