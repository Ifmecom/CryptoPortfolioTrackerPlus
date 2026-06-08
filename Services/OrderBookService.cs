using System.Text.Json;
using CryptoPortfolioTracker.Models;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt top-20 orderboek op van Binance spot API en berekent spread + diepte.
/// Gebruikt de gedeelde <see cref="TtlCache{T}"/> (60s) tegen rate-limits.
/// </summary>
public class OrderBookService : IOrderBookService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(OrderBookService).PadRight(22));

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private const string BaseUrl = "https://api.binance.com/api/v3/depth";

    private readonly TtlCache<OrderBookSnapshot?> _cache = new(TimeSpan.FromSeconds(60));

    public async Task<OrderBookSnapshot?> GetSnapshotAsync(string binanceSymbol, CancellationToken ct = default)
    {
        var sym = binanceSymbol.ToUpperInvariant();

        if (_cache.TryGet(sym, out var cached))
            return cached;

        try
        {
            var url      = $"{BaseUrl}?symbol={sym}&limit=20";
            var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("OrderBook: {Symbol} → {Code}", sym, (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            // Parse bids / asks: [[price, qty], ...]
            double bidDepth = SumDepth(doc.RootElement, "bids", 5);
            double askDepth = SumDepth(doc.RootElement, "asks", 5);

            double bestBid  = ParseFirst(doc.RootElement, "bids");
            double bestAsk  = ParseFirst(doc.RootElement, "asks");
            double mid      = (bestBid + bestAsk) / 2.0;
            double spread   = mid > 0 ? (bestAsk - bestBid) / mid * 100.0 : 0;

            var snap = new OrderBookSnapshot(sym, Math.Round(spread, 4), bidDepth, askDepth);
            _cache.Set(sym, snap);

            Logger.Debug("OrderBook {Symbol}: spread={Spread:0.000}%, bidDepth={Bid:N0}$, askDepth={Ask:N0}$",
                sym, spread, bidDepth, askDepth);

            return snap;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Warning(ex, "OrderBook: failed to fetch {Symbol}", sym);
            return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double SumDepth(JsonElement root, string side, int levels)
    {
        if (!root.TryGetProperty(side, out var arr)) return 0;
        double total = 0;
        int count = 0;
        foreach (var entry in arr.EnumerateArray())
        {
            if (count++ >= levels) break;
            double price = double.Parse(entry[0].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            double qty   = double.Parse(entry[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            total += price * qty;
        }
        return total;
    }

    private static double ParseFirst(JsonElement root, string side)
    {
        if (!root.TryGetProperty(side, out var arr)) return 0;
        foreach (var entry in arr.EnumerateArray())
            return double.Parse(entry[0].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return 0;
    }
}
