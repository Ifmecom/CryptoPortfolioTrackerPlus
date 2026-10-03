using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>Eén ontvangen TradingView-alert (via de ntfy.sh-doorgeefdienst).</summary>
/// <param name="Id">ntfy-bericht-id (cursor voor de volgende ophaalronde).</param>
/// <param name="Time">Ontvangsttijd bij ntfy (UTC).</param>
/// <param name="Event">"entry", "stoploss", "tp1" of "custom" (geen CPT-bericht).</param>
/// <param name="Pair">Paar zonder beursprefix, bijv. "LUNCUSDT" (leeg als onbekend).</param>
/// <param name="Exchange">Beurs volgens TradingView, bijv. "BINANCE" (leeg als onbekend).</param>
/// <param name="Price">Slotkoers op het alertmoment (0 als onbekend).</param>
/// <param name="Text">Het ruwe bericht (ingekort).</param>
public sealed record TradingViewAlert(string Id, DateTime Time, string Event, string Pair, string Exchange, double Price, string Text)
{
    public bool IsCpt => Event is "entry" or "stoploss" or "tp1";

    public string EventLabel => Event switch
    {
        "entry"    => "Entry geraakt",
        "stoploss" => "Stop-loss geraakt",
        "tp1"      => "TP1 geraakt",
        _          => "Alert",
    };
}

/// <summary>
/// TradingView-webhooks (v1.48) — puur en getest. TradingView post het alertbericht naar
/// <c>https://ntfy.sh/{kanaal}</c>; de app haalt de berichten op met
/// <c>GET https://ntfy.sh/{kanaal}/json?poll=1&amp;since={id}</c> (één JSON-object per regel).
/// Het kanaal is geheim omdat de naam willekeurig en lang is — wie de naam kent, kan meelezen en posten.
/// </summary>
public static class TradingViewAlerts
{
    public const string NtfyBaseUrl = "https://ntfy.sh/";
    public const int MaxTextLength = 500;

    private static readonly Regex TopicPattern = new("^[A-Za-z0-9_-]{16,64}$", RegexOptions.Compiled);

    /// <summary>Nieuw geheim kanaal, bijv. "cpt-3f9a…" (4 + 32 hex-tekens = 128 bit).</summary>
    public static string NewTopic() => "cpt-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>Lang genoeg en alleen veilige tekens (ntfy staat letters, cijfers, _ en - toe).</summary>
    public static bool IsValidTopic(string? topic) => !string.IsNullOrWhiteSpace(topic) && TopicPattern.IsMatch(topic);

    /// <summary>De URL die je in TradingView bij 'Webhook-URL' invult.</summary>
    public static string WebhookUrl(string topic) => NtfyBaseUrl + topic;

    /// <summary>Ophaal-URL; zonder cursor de berichten van het laatste <paramref name="firstWindow"/>.</summary>
    public static string PollUrl(string topic, string? sinceId, string firstWindow = "10m")
        => $"{NtfyBaseUrl}{topic}/json?poll=1&since={Uri.EscapeDataString(string.IsNullOrWhiteSpace(sinceId) ? firstWindow : sinceId)}";

    /// <summary>
    /// Leest het ntfy-antwoord (één JSON-object per regel). Alleen <c>"event":"message"</c> telt; kapotte regels
    /// worden overgeslagen. Volgorde = volgorde van ntfy (oud → nieuw).
    /// </summary>
    public static List<TradingViewAlert> ParsePoll(string? body)
    {
        var result = new List<TradingViewAlert>();
        if (string.IsNullOrWhiteSpace(body)) return result;

        foreach (var line in body.Split('\n'))
        {
            var l = line.Trim();
            if (l.Length == 0 || l[0] != '{') continue;
            try
            {
                using var doc = JsonDocument.Parse(l);
                var root = doc.RootElement;
                if (Str(root, "event") != "message") continue;
                var id = Str(root, "id");
                if (string.IsNullOrEmpty(id)) continue;
                var time = root.TryGetProperty("time", out var t) && t.TryGetInt64(out var unix)
                    ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                    : DateTime.UtcNow;
                result.Add(ParseMessage(id, time, Str(root, "message")));
            }
            catch (JsonException) { /* regel overslaan */ }
        }
        return result;
    }

    /// <summary>
    /// Leest het alertbericht van een CPT-script: <c>{"bron":"CPT","event":"entry","ticker":"LUNCUSDT","prijs":0.0001}</c>.
    /// Ander tekst (eigen alerts van de gebruiker) wordt een "custom"-alert met de tekst als inhoud.
    /// </summary>
    public static TradingViewAlert ParseMessage(string id, DateTime time, string? message)
    {
        var text = (message ?? string.Empty).Trim();
        var shortText = text.Length > MaxTextLength ? text[..MaxTextLength] + "…" : text;

        if (text.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var evt = Str(root, "event").ToLowerInvariant();
                    var ticker = FirstNonEmpty(Str(root, "paar"), Str(root, "ticker"));
                    var exchange = Str(root, "exchange").ToUpperInvariant();
                    if (ticker.Contains(':'))
                    {
                        if (exchange.Length == 0) exchange = ticker[..ticker.IndexOf(':')].ToUpperInvariant();
                        ticker = TradingViewSymbol.PairOf(ticker);
                    }
                    bool cpt = string.Equals(Str(root, "bron"), "CPT", StringComparison.OrdinalIgnoreCase)
                               && evt is "entry" or "stoploss" or "tp1";
                    return new TradingViewAlert(id, time, cpt ? evt : "custom", ticker.ToUpperInvariant(), exchange,
                        Num(root, "prijs") is var p && p > 0 ? p : Num(root, "price"), shortText);
                }
            }
            catch (JsonException) { /* geen geldige JSON: valt terug op custom */ }
        }

        return new TradingViewAlert(id, time, "custom", string.Empty, string.Empty, 0, shortText);
    }

    /// <summary>
    /// Basissymbool uit een paar: "LUNCUSDT" → "LUNC". Probeert de quote-munten in volgorde; zonder bekende quote
    /// het paar zelf.
    /// </summary>
    public static string BaseSymbol(string? pair, params string[] quotes)
    {
        var p = TradingViewSymbol.PairOf((pair ?? string.Empty).Trim().ToUpperInvariant());
        var qs = (quotes.Length > 0 ? quotes : new[] { "USDT", "USDC", "BUSD", "FDUSD", "USD", "EUR", "BTC" })
            .Where(q => !string.IsNullOrWhiteSpace(q)).Select(q => q.Trim().ToUpperInvariant());
        foreach (var q in qs)
            if (p.Length > q.Length && p.EndsWith(q, StringComparison.Ordinal))
                return p[..^q.Length];
        return p;
    }

    /// <summary>
    /// Mag een 'Entry geraakt'-alert een Bybit Demo-order plaatsen? Alleen als de gebruiker het aanzette, het een
    /// CPT-entry-alert is, er een gevolgde Long-setup voor die munt is die nog op Watching staat (Bybit Demo is
    /// spot: geen Short), er nog geen open demo-order voor die munt is en het dagmaximum niet bereikt is.
    /// </summary>
    public static (bool Place, string Reason) AutoOrderCheck(
        TradingViewAlert alert, Models.WatchedSetup? setup, bool enabled, bool hasOpenOrder, int placedToday, int maxPerDay)
    {
        if (!enabled)                    return (false, "automatische order staat uit");
        if (alert.Event != "entry")      return (false, "geen entry-alert");
        if (setup is null)               return (false, "geen gevolgde setup voor deze munt in de Setup Tracker");
        if (!string.Equals(setup.Direction, "Long", StringComparison.OrdinalIgnoreCase))
                                         return (false, "Short-setup — Bybit Demo handelt alleen spot (Long)");
        if (setup.Status != Enums.WatchedSetupStatus.Watching)
                                         return (false, "setup loopt al of is afgesloten");
        if (setup.EntryPrice <= 0 || setup.StopLoss <= 0 || setup.Target1 <= 0 || setup.StopLoss >= setup.EntryPrice)
                                         return (false, "setup heeft geen geldige entry/stop-loss/TP1");
        if (setup.LinkedOrderId is not null) return (false, "er hangt al een order aan deze setup");
        if (hasOpenOrder)                return (false, "er staat al een demo-order open voor deze munt");
        if (placedToday >= maxPerDay)    return (false, $"dagmaximum automatische orders bereikt ({maxPerDay})");
        return (true, "entry geraakt op een gevolgde Long-setup");
    }

    /// <summary>Telegram-bericht (HTML) voor een alert, met een vermelding als hij bij een gevolgde setup hoort.</summary>
    public static string FormatTelegram(TradingViewAlert a, string? setupInfo = null)
    {
        string icon = a.Event switch { "entry" => "🎯", "stoploss" => "🛑", "tp1" => "✅", _ => "🔔" };
        var lines = new List<string> { $"<b>{icon} TradingView: {Html(a.EventLabel)}</b>" };
        if (a.Pair.Length > 0)
            lines.Add($"{Html(a.Pair)}{(a.Exchange.Length > 0 ? $" ({Html(a.Exchange)})" : "")}{(a.Price > 0 ? $" @ {a.Price.ToString("0.########", CultureInfo.InvariantCulture)}" : "")}");
        if (!a.IsCpt && a.Text.Length > 0) lines.Add(Html(a.Text));
        if (!string.IsNullOrWhiteSpace(setupInfo)) lines.Add(Html(setupInfo));
        return string.Join("\n", lines);
    }

    /// <summary>Korte regel voor het overzicht in de app.</summary>
    public static string Describe(TradingViewAlert a)
        => $"{a.Time.ToLocalTime():dd-MM HH:mm} · {a.EventLabel}" +
           (a.Pair.Length > 0 ? $" · {a.Pair}" : "") +
           (a.Price > 0 ? $" @ {a.Price.ToString("0.########", CultureInfo.InvariantCulture)}" : "") +
           (!a.IsCpt && a.Text.Length > 0 ? $" · {(a.Text.Length > 80 ? a.Text[..80] + "…" : a.Text)}" : "");

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            _ => string.Empty,
        } : string.Empty;

    private static double Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String &&
            double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return s;
        return 0;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static string Html(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
