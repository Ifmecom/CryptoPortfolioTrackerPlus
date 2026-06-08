using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Haalt TVL-data op via DefiLlama (gratis GET https://api.llama.fi/protocols).
/// De volledige protocollijst wordt éénmalig opgehaald en gecached; per coin
/// wordt lokaal gematcht op CoinGecko-id (met symbool als fallback).
/// </summary>
public class DefiLlamaService : IDefiLlamaService
{
    private static readonly ILogger Logger = Log.Logger.ForContext(
        Constants.SourceContextPropertyName, nameof(DefiLlamaService).PadRight(22));

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "CryptoPortfolioTrackerPlus/1.0" } },
    };

    private const string ProtocolsUrl = "https://api.llama.fi/protocols";
    private const int CacheTtlMinutes = 30;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, DefiLlamaInfo> _byGecko = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, DefiLlamaInfo> _bySymbol = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _expiry = DateTime.MinValue;

    public async Task<DefiLlamaInfo?> GetInfoAsync(string geckoId, string symbol, CancellationToken ct = default)
    {
        if (!await EnsureLoadedAsync(ct)) return null;

        if (!string.IsNullOrWhiteSpace(geckoId) && _byGecko.TryGetValue(geckoId, out var byId))
            return byId;
        if (!string.IsNullOrWhiteSpace(symbol) && _bySymbol.TryGetValue(symbol, out var bySym))
            return bySym;
        return null;
    }

    private async Task<bool> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_byGecko.Count > 0 && DateTime.UtcNow < _expiry) return true;

        await _gate.WaitAsync(ct);
        try
        {
            if (_byGecko.Count > 0 && DateTime.UtcNow < _expiry) return true;

            var resp = await _http.GetAsync(ProtocolsUrl, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Logger.Warning("DefiLlama: /protocols gaf {Status}", (int)resp.StatusCode);
                return _byGecko.Count > 0; // gebruik eventueel verlopen cache
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var list = ParseProtocols(json);

            // Bij dubbele id's/symbolen: kies het protocol met de hoogste TVL (de hoofd-deployment).
            _byGecko = list.Where(p => !string.IsNullOrWhiteSpace(p.GeckoId))
                           .GroupBy(p => p.GeckoId, StringComparer.OrdinalIgnoreCase)
                           .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Tvl).First(), StringComparer.OrdinalIgnoreCase);
            _bySymbol = list.Where(p => !string.IsNullOrWhiteSpace(p.Symbol) && p.Symbol != "-")
                            .GroupBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Tvl).First(), StringComparer.OrdinalIgnoreCase);
            _expiry = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);

            Logger.Debug("DefiLlama: {Count} protocollen geladen", list.Count);
            return _byGecko.Count > 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Warning(ex, "DefiLlama: laden mislukt");
            return _byGecko.Count > 0;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Pure parser van de /protocols-respons (testbaar zonder netwerk).</summary>
    public static List<DefiLlamaInfo> ParseProtocols(string json)
    {
        var result = new List<DefiLlamaInfo>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            string name     = GetString(el, "name");
            string symbol   = GetString(el, "symbol");
            string geckoId  = GetString(el, "gecko_id");
            string category = GetString(el, "category");
            double tvl      = GetDouble(el, "tvl");
            double? mcap    = GetNullableDouble(el, "mcap");

            result.Add(new DefiLlamaInfo(name, symbol, geckoId, category, tvl, mcap));
        }
        return result;
    }

    private static string GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double GetDouble(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : 0;

    private static double? GetNullableDouble(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;
}
