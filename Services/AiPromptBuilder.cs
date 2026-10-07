using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Bouwt de systeemprompt en de portfolio-context voor de AI-vraag, en leest het optionele
/// <c>```acties</c>-blok uit het antwoord. Puur en getest.
/// </summary>
public static class AiPromptBuilder
{
    public const string ActionFence = "acties";

    /// <summary>Systeemprompt. Bewust stabiel (geen datum/tijd), zodat aanbieders hem kunnen cachen.</summary>
    public const string SystemPrompt =
        "Je bent een crypto-onderzoeksassistent in de desktop-app Crypto Portfolio Tracker. " +
        "Antwoord in het Nederlands, helder en feitelijk. Noem onzekerheden en risico's en maak duidelijk wat feit is en wat inschatting. " +
        "Je geeft geen persoonlijk financieel advies en geen garanties; de gebruiker beslist zelf. " +
        "Schrijf munten met hun ticker in hoofdletters (BTC, ETH, SOL). Als je weet dat iets na je kennisdatum kan zijn veranderd, zeg dat.\n\n" +
        "Als je antwoord een concrete vervolgstap in de app oplevert, sluit dan af met precies één codeblok met als taal '" + ActionFence + "', " +
        "met een JSON-array van acties. Toegestane velden: " +
        "\"actie\" (een van: koop, verkoop, paper_long, paper_short, prijsniveaus, toevoegen), \"symbool\" (ticker), " +
        "en waar van toepassing \"entry\", \"stop\", \"doel\" (koersen in USD als getal, punt als decimaalteken, geen duizendtalscheiding) en \"reden\" (max. 12 woorden). " +
        "Voorbeeld:\n```" + ActionFence + "\n[{\"actie\":\"paper_long\",\"symbool\":\"SOL\",\"entry\":142.5,\"stop\":131,\"doel\":165,\"reden\":\"retest van steun\"}]\n```\n" +
        "Voeg alleen acties toe die direct uit de vraag en je antwoord volgen; laat het blok weg als er geen zinnige actie is. " +
        "De app voert niets automatisch uit: de gebruiker bevestigt elke actie zelf.";

    /// <summary>Portfolio-context als platte tekst: posities op waarde, plus overige munten in de bibliotheek.</summary>
    public static string BuildContext(IReadOnlyList<AiHolding> holdings, IReadOnlyList<AiCoinRef> library, DateTime nowLocal,
                                      int maxHoldings = 25, int maxWatch = 40)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Context uit de app (koersen in USD, {nowLocal:dd-MM-yyyy HH:mm}):");

        var rows = holdings.Where(h => h.Qty > 0).OrderByDescending(h => h.Value).ToList();
        if (rows.Count == 0)
            sb.AppendLine("- Geen posities in het portfolio.");
        else
        {
            double total = rows.Sum(h => h.Value);
            sb.AppendLine(Invariant($"Portfolio: {rows.Count} posities, totale waarde ${total:#,0}."));
            foreach (var h in rows.Take(maxHoldings))
            {
                var share = total > 0 ? h.Value / total * 100 : 0;
                var pnl = h.AverageCostPrice > 0 ? $", resultaat {h.PnlPct:+0;-0}%" : string.Empty;
                sb.AppendLine(Invariant($"- {h.Symbol.ToUpperInvariant()} ({h.Name}): {h.Qty:0.####} stuks, koers {Price(h.Price)}, waarde ${h.Value:#,0} ({share:0.#}%){pnl}"));
            }
            if (rows.Count > maxHoldings)
                sb.AppendLine($"- en nog {rows.Count - maxHoldings} kleinere posities.");
        }

        var held = new HashSet<string>(rows.Select(h => h.Symbol), StringComparer.OrdinalIgnoreCase);
        var watch = library.Where(c => !held.Contains(c.Symbol) && !string.IsNullOrWhiteSpace(c.Symbol))
                           .Select(c => c.Symbol.ToUpperInvariant()).Distinct().Take(maxWatch).ToList();
        if (watch.Count > 0)
            sb.AppendLine("Ook gevolgd (bibliotheek, niet in bezit): " + string.Join(", ", watch) + ".");

        return sb.ToString().TrimEnd();
    }

    /// <summary>De vraag zoals hij naar de AI gaat, met of zonder context.</summary>
    public static string BuildUserMessage(string question, string? context) =>
        string.IsNullOrWhiteSpace(context) ? question.Trim() : $"{context}\n\nVraag: {question.Trim()}";

    private static readonly Regex Fence = new(
        @"```[ \t]*(?:" + ActionFence + @"|json)[ \t]*\r?\n(?<body>[\s\S]*?)```", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Haal het actieblok uit het antwoord. Geeft de tekst zonder blok terug en de acties die erin stonden
    /// (alleen bekende acties; onbekende munten worden 'toevoegen'). Kapotte JSON = geen acties, tekst blijft.
    /// </summary>
    public static (string Text, List<SmartAction> Actions) ExtractActions(string? answer, IReadOnlyList<AiCoinRef> coins)
    {
        var actions = new List<SmartAction>();
        if (string.IsNullOrEmpty(answer)) return (string.Empty, actions);

        Match? block = null;
        foreach (Match m in Fence.Matches(answer))
            if (m.Value.Contains(ActionFence, StringComparison.OrdinalIgnoreCase) || m.Groups["body"].Value.Contains("\"actie\""))
                block = m;                                                       // laatste blok wint
        if (block is null) return (answer.Trim(), actions);

        var text = answer.Remove(block.Index, block.Length).Trim();
        try
        {
            using var doc = JsonDocument.Parse(block.Groups["body"].Value);
            var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList()
                      : doc.RootElement.ValueKind == JsonValueKind.Object ? new List<JsonElement> { doc.RootElement }
                      : new List<JsonElement>();
            foreach (var item in items)
                if (ToAction(item, coins) is { } a) actions.Add(a);
        }
        catch (JsonException)
        {
            // Geen geldige JSON: dan alleen de herkenning uit de tekst.
        }
        return (text, actions);
    }

    private static SmartAction? ToAction(JsonElement e, IReadOnlyList<AiCoinRef> coins)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var kind = Str(e, "actie").ToLowerInvariant();
        var symbol = Str(e, "symbool").Trim().TrimStart('$').ToUpperInvariant();
        if (symbol.Length == 0 || TradeSetupGate.IsStablecoin(symbol)) return null;
        var reason = Str(e, "reden");

        var coin = coins.Where(c => c.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(c => c.IsAsset).FirstOrDefault();
        if (coin is null)
            return new SmartAction
            {
                Kind = SmartActionKind.AddCoin, Symbol = symbol, SearchText = symbol,
                Reason = string.IsNullOrWhiteSpace(reason) ? "voorgesteld door de AI, niet in je bibliotheek" : reason,
                Source = "AI-voorstel",
            };

        var a = new SmartAction
        {
            Symbol = symbol, CoinApiId = coin.ApiId, CoinName = coin.Name, Reason = reason, Source = "AI-voorstel",
        };
        double? entry = Num(e, "entry"), stop = Num(e, "stop"), target = Num(e, "doel");

        switch (kind)
        {
            case "koop":     return a with { Kind = SmartActionKind.Buy };
            case "verkoop":  return coin.IsAsset ? a with { Kind = SmartActionKind.Sell } : null;
            case "toevoegen": return null;                                  // staat al in de bibliotheek
            case "prijsniveaus":
                return a with { Kind = SmartActionKind.PriceLevels, Entry = entry, StopLoss = stop, Target = target };
            case "paper_long":
            case "paper_short":
                var dir = kind == "paper_long" ? "Long" : "Short";
                var (pe, ps, pt) = AiIntentDetector.ValidLevels(dir, entry ?? (coin.Price > 0 ? coin.Price : null), stop, target);
                return a with { Kind = SmartActionKind.PaperTrade, Direction = dir, Entry = entry is null ? null : pe, StopLoss = ps, Target = pt };
            default: return null;
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static double? Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d > 0 ? d : null;
        if (v.ValueKind == JsonValueKind.String) return AiNumberParser.ParseFirst(v.GetString() ?? string.Empty);
        return null;
    }

    private static string Price(double p) => "$" + p.ToString(p switch
    {
        >= 1000 => "#,0",
        >= 1    => "0.##",
        _       => "0.######",
    }, CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
