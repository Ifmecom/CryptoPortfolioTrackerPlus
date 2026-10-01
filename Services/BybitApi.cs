using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure hulpfuncties voor de Bybit V5 REST API (v1.47): ondertekenen, query-strings bouwen en
/// antwoorden parsen. Geen I/O — volledig testbaar. De HTTP-kant zit in <see cref="BybitDemoExecutor"/>.
/// </summary>
public static class BybitApi
{
    public const int RecvWindow = 5000;

    /// <summary>Kandidaat-domeinen voor Bybit Demo Trading, in volgorde van proberen.</summary>
    public static IReadOnlyList<string> DemoBaseUrlCandidates(bool preferEu) => preferEu
        ? new[] { "https://api-demo.bybit.eu", "https://api-demo.bybit.com" }
        : new[] { "https://api-demo.bybit.com", "https://api-demo.bybit.eu" };

    // ── Ondertekening (HMAC, sign-type 2) ───────────────────────────────────

    /// <summary>
    /// Bybit V5-handtekening: HMAC-SHA256 over <c>timestamp + apiKey + recvWindow + payload</c>, lowercase hex.
    /// Payload = query-string (GET) of JSON-body (POST), exact zoals verstuurd.
    /// </summary>
    public static string Sign(string secret, long timestampMs, string apiKey, int recvWindow, string payload)
    {
        var message = $"{timestampMs}{apiKey}{recvWindow}{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    /// <summary>Query-string in vaste volgorde (de volgorde moet gelijk zijn aan wat ondertekend wordt).</summary>
    public static string Query(params (string Key, string? Value)[] parts)
        => string.Join("&", parts
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));

    // ── Getallen ────────────────────────────────────────────────────────────

    public static decimal Dec(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0m;
        return v.ValueKind switch
        {
            JsonValueKind.String => decimal.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            JsonValueKind.Number => v.TryGetDecimal(out var n) ? n : 0m,
            _ => 0m,
        };
    }

    public static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty
         : e.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Number ? n.ToString()
         : string.Empty;

    public static long Long(JsonElement e, string name)
        => long.TryParse(Str(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0;

    /// <summary>Formatteert een decimal zonder exponent en zonder overbodige nullen (invariant).</summary>
    public static string Num(decimal value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);

    // ── Antwoorden ──────────────────────────────────────────────────────────

    /// <summary>Leest retCode/retMsg; retourneert het <c>result</c>-element bij succes.</summary>
    public static (int RetCode, string RetMsg, JsonElement? Result) ReadEnvelope(JsonDocument doc)
    {
        var root = doc.RootElement;
        int code = root.TryGetProperty("retCode", out var c) && c.TryGetInt32(out var ci) ? ci : -1;
        string msg = root.TryGetProperty("retMsg", out var m) ? m.GetString() ?? string.Empty : string.Empty;
        JsonElement? result = root.TryGetProperty("result", out var r) ? r : null;
        return (code, msg, result);
    }

    private static IEnumerable<JsonElement> List(JsonElement result)
        => result.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    public static BybitSpotInstrument? ParseSpotInstrument(JsonElement result, string symbol)
    {
        foreach (var it in List(result))
        {
            if (!string.Equals(Str(it, "symbol"), symbol, StringComparison.OrdinalIgnoreCase)) continue;

            var lot   = it.TryGetProperty("lotSizeFilter", out var l) ? l : default;
            var price = it.TryGetProperty("priceFilter",   out var p) ? p : default;
            return new BybitSpotInstrument(
                Symbol:         Str(it, "symbol"),
                BaseCoin:       Str(it, "baseCoin"),
                QuoteCoin:      Str(it, "quoteCoin"),
                Status:         Str(it, "status"),
                BasePrecision:  lot.ValueKind   == JsonValueKind.Object ? Dec(lot, "basePrecision")  : 0m,
                QuotePrecision: lot.ValueKind   == JsonValueKind.Object ? Dec(lot, "quotePrecision") : 0m,
                MinOrderQty:    lot.ValueKind   == JsonValueKind.Object ? Dec(lot, "minOrderQty")    : 0m,
                MinOrderAmt:    lot.ValueKind   == JsonValueKind.Object ? Dec(lot, "minOrderAmt")    : 0m,
                TickSize:       price.ValueKind == JsonValueKind.Object ? Dec(price, "tickSize")     : 0m);
        }
        return null;
    }

    /// <summary>Beste vraagprijs (ask) uit de ticker, of de laatste prijs als ask ontbreekt.</summary>
    public static decimal ParseAskPrice(JsonElement result, string symbol)
    {
        foreach (var it in List(result))
        {
            if (!string.Equals(Str(it, "symbol"), symbol, StringComparison.OrdinalIgnoreCase)) continue;
            var ask = Dec(it, "ask1Price");
            return ask > 0 ? ask : Dec(it, "lastPrice");
        }
        return 0m;
    }

    public static List<BybitOrderInfo> ParseOrders(JsonElement result)
        => List(result).Select(o => new BybitOrderInfo(
                OrderId:       Str(o, "orderId"),
                OrderLinkId:   Str(o, "orderLinkId"),
                Symbol:        Str(o, "symbol"),
                Side:          Str(o, "side"),
                OrderStatus:   Str(o, "orderStatus"),
                Price:         Dec(o, "price"),
                Qty:           Dec(o, "qty"),
                AvgPrice:      Dec(o, "avgPrice"),
                CumExecQty:    Dec(o, "cumExecQty"),
                CumExecValue:  Dec(o, "cumExecValue"),
                TakeProfit:    Dec(o, "takeProfit"),
                StopLoss:      Dec(o, "stopLoss"),
                RejectReason:  Str(o, "rejectReason"),
                CreatedTimeMs: Long(o, "createdTime"),
                UpdatedTimeMs: Long(o, "updatedTime")))
            .ToList();

    public static List<BybitExecution> ParseExecutions(JsonElement result)
        => List(result).Select(x => new BybitExecution(
                ExecId:        Str(x, "execId"),
                OrderId:       Str(x, "orderId"),
                OrderLinkId:   Str(x, "orderLinkId"),
                Symbol:        Str(x, "symbol"),
                Side:          Str(x, "side"),
                ExecPrice:     Dec(x, "execPrice"),
                ExecQty:       Dec(x, "execQty"),
                ExecFee:       Dec(x, "execFee"),
                FeeCurrency:   Str(x, "feeCurrency"),
                StopOrderType: Str(x, "stopOrderType"),
                ExecTimeMs:    Long(x, "execTime")))
            .ToList();

    /// <summary>Saldi per munt uit <c>/v5/account/wallet-balance?accountType=UNIFIED</c>.</summary>
    public static List<BybitCoinBalance> ParseWallet(JsonElement result)
    {
        var balances = new List<BybitCoinBalance>();
        foreach (var acc in List(result))
        {
            if (!acc.TryGetProperty("coin", out var coins) || coins.ValueKind != JsonValueKind.Array) continue;
            foreach (var c in coins.EnumerateArray())
                balances.Add(new BybitCoinBalance(Str(c, "coin"), Dec(c, "walletBalance"), Dec(c, "locked")));
        }
        return balances;
    }

    /// <summary>Cursor voor de volgende pagina, of leeg.</summary>
    public static string NextCursor(JsonElement result)
        => result.TryGetProperty("nextPageCursor", out var c) ? c.GetString() ?? string.Empty : string.Empty;

    /// <summary>
    /// Vertaalt veelvoorkomende Bybit-foutcodes naar een begrijpelijke Nederlandse uitleg.
    /// </summary>
    public static string ExplainError(int retCode, string retMsg) => retCode switch
    {
        10003 => "API-sleutel ongeldig of hoort bij een ander domein (demo/live, EU/global).",
        10004 => "Handtekening klopt niet — controleer de API Secret.",
        10005 => "API-sleutel heeft geen handelsrechten (zet 'Trade' / 'Spot' aan bij de sleutel).",
        10010 => "IP-adres staat niet op de whitelist van deze API-sleutel.",
        10002 => "Tijd van de pc wijkt te veel af van Bybit — synchroniseer de Windows-klok.",
        170131 => "Onvoldoende saldo.",
        170136 => "Order is groter dan het maximum voor dit paar.",
        170140 => "Orderwaarde is lager dan het minimum voor dit paar.",
        _ => retMsg,
    } + $" (Bybit {retCode})";
}
