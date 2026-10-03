using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoPortfolioTracker.Services;

/// <summary>Eén setup zoals de <see cref="PineScriptGenerator"/> hem in een Pine Script zet (v1.48).</summary>
/// <param name="Ticker">TradingView-ticker, bijv. "BINANCE:LUNCUSDT" (zie <see cref="TradingViewSymbol"/>).</param>
/// <param name="Name">Naam van de munt (voor titel en labels).</param>
/// <param name="Direction">"Long" of "Short".</param>
/// <param name="Target2">0 = geen tweede doel.</param>
/// <param name="Source">Pagina waar de setup vandaan komt (bijv. "Pattern Trading").</param>
/// <param name="Note">Korte toelichting (patronen, kansscore) — komt als commentaar in het script.</param>
public sealed record PineSetup(
    string Ticker,
    string Name,
    string Direction,
    double Entry,
    double StopLoss,
    double Target1,
    double Target2 = 0,
    int    Score   = 0,
    string Source  = "",
    string Note    = "",
    IReadOnlyList<double>? Supports    = null,
    IReadOnlyList<double>? Resistances = null)
{
    public bool IsLong  => string.Equals(Direction, "Long", StringComparison.OrdinalIgnoreCase);
    public bool IsValid => (IsLong || string.Equals(Direction, "Short", StringComparison.OrdinalIgnoreCase))
                           && Entry > 0 && StopLoss > 0 && Target1 > 0 && !string.IsNullOrWhiteSpace(Ticker);
}

/// <summary>
/// Genereert TradingView Pine Script (v6) uit setups van de app (v1.48). Puur en getest.
/// <list type="bullet">
/// <item><see cref="ForSetup"/>: één setup — lijnen voor entry/SL/TP1/TP2, risico- en winstzone, steun/weerstand,
///   labels en drie alert-condities (entry geraakt, stop-loss geraakt, TP1 geraakt).</item>
/// <item><see cref="ForWatchlist"/>: meerdere setups in één script — kiest op basis van het grafieksymbool
///   (<c>syminfo.ticker</c>) welke niveaus het tekent. Eén keer toevoegen, dan door de watchlist bladeren.</item>
/// </list>
/// Alertberichten zijn JSON, zodat ze later ook als webhook bruikbaar zijn.
/// </summary>
public static class PineScriptGenerator
{
    public const string PineVersion = "6";
    /// <summary>Maximaal aantal steun-/weerstandsniveaus per kant in een setup-script.</summary>
    public const int MaxLevelsPerSide = 4;
    /// <summary>Maximaal aantal setups in één watchlist-script (houdt het script klein en leesbaar).</summary>
    public const int MaxWatchlistSetups = 40;

    // ── Eén setup ───────────────────────────────────────────────────────────

    public static string ForSetup(PineSetup s, DateTime? generatedAt = null)
    {
        if (!s.IsValid) throw new ArgumentException("Setup is onvolledig (richting, entry, stop-loss, TP1 en ticker zijn nodig).", nameof(s));

        var name = Safe(s.Name);
        var pair = TradingViewSymbol.PairOf(s.Ticker);
        var sb   = new StringBuilder();

        Header(sb, $"setup voor {name} ({s.Ticker}) — {s.Direction}", generatedAt, s.Source,
            $"Richting {s.Direction}{(s.Score > 0 ? $", score {s.Score}" : "")}. R/R naar TP1 1:{F(RiskReward(s), 2)}.",
            s.Note,
            $"Gebruik: open {s.Ticker} op TradingView → Pine Editor → plak dit script → Opslaan → Toevoegen aan grafiek.");
        sb.AppendLine($"indicator(\"CPT · {name} {s.Direction}\", overlay = true, max_labels_count = 20)");
        sb.AppendLine();
        sb.AppendLine($"const float  ENTRY   = {P(s.Entry)}");
        sb.AppendLine($"const float  SL      = {P(s.StopLoss)}");
        sb.AppendLine($"const float  TP1     = {P(s.Target1)}");
        if (s.Target2 > 0) sb.AppendLine($"const float  TP2     = {P(s.Target2)}");
        sb.AppendLine($"const bool   IS_LONG = {(s.IsLong ? "true" : "false")}");
        sb.AppendLine($"const string PAIR    = \"{pair}\"");
        sb.AppendLine();
        sb.AppendLine("// ── Niveaus ──────────────────────────────────────────────");
        sb.AppendLine("hEntry = hline(ENTRY, \"Entry\",     color.new(color.blue, 0),  hline.style_solid,  2)");
        sb.AppendLine("hSl    = hline(SL,    \"Stop-loss\", color.new(color.red, 0),   hline.style_solid,  2)");
        sb.AppendLine("hTp1   = hline(TP1,   \"TP1\",       color.new(color.green, 0), hline.style_solid,  2)");
        if (s.Target2 > 0)
            sb.AppendLine("hTp2   = hline(TP2,   \"TP2\",       color.new(color.green, 30), hline.style_dashed, 1)");
        sb.AppendLine("fill(hEntry, hSl,  color.new(color.red, 88),   \"Risicozone\")");
        sb.AppendLine("fill(hEntry, hTp1, color.new(color.green, 88), \"Winstzone\")");

        var supports    = (s.Supports    ?? Array.Empty<double>()).Where(v => v > 0).Distinct().OrderByDescending(v => v).Take(MaxLevelsPerSide).ToList();
        var resistances = (s.Resistances ?? Array.Empty<double>()).Where(v => v > 0).Distinct().OrderBy(v => v).Take(MaxLevelsPerSide).ToList();
        if (supports.Count + resistances.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// ── Steun en weerstand (uit de analyse van de app) ───────");
            foreach (var v in supports)    sb.AppendLine($"hline({P(v)}, \"Steun\",    color.new(color.teal, 40),   hline.style_dotted, 1)");
            foreach (var v in resistances) sb.AppendLine($"hline({P(v)}, \"Weerstand\", color.new(color.orange, 40), hline.style_dotted, 1)");
        }

        sb.AppendLine();
        sb.AppendLine("// ── Labels (één keer, op de laatste afgesloten bar) ─────");
        sb.AppendLine("if barstate.islastconfirmedhistory");
        sb.AppendLine("    label.new(bar_index + 3, ENTRY, \"Entry \" + str.tostring(ENTRY, format.mintick), style = label.style_label_left, color = color.blue,  textcolor = color.white, size = size.small)");
        sb.AppendLine("    label.new(bar_index + 3, SL,    \"SL \"    + str.tostring(SL,    format.mintick), style = label.style_label_left, color = color.red,   textcolor = color.white, size = size.small)");
        sb.AppendLine("    label.new(bar_index + 3, TP1,   \"TP1 \"   + str.tostring(TP1,   format.mintick), style = label.style_label_left, color = color.green, textcolor = color.white, size = size.small)");
        if (s.Target2 > 0)
            sb.AppendLine("    label.new(bar_index + 3, TP2,   \"TP2 \"   + str.tostring(TP2,   format.mintick), style = label.style_label_left, color = color.green, textcolor = color.white, size = size.small)");
        sb.AppendLine("    if syminfo.ticker != PAIR");
        sb.AppendLine("        label.new(bar_index, high, \"Let op: dit script is gemaakt voor \" + PAIR + \", niet voor \" + syminfo.ticker, style = label.style_label_down, color = color.orange, textcolor = color.black, size = size.normal)");

        sb.AppendLine();
        sb.AppendLine("// ── Alerts: ⋯ op de grafiek → Alert toevoegen → Conditie: dit script → kies de gebeurtenis ──");
        sb.AppendLine("entryHit = low <= ENTRY and high >= ENTRY");
        sb.AppendLine("slHit    = IS_LONG ? low <= SL  : high >= SL");
        sb.AppendLine("tp1Hit   = IS_LONG ? high >= TP1 : low <= TP1");
        sb.AppendLine($"alertcondition(entryHit, \"Entry geraakt\",     {AlertMessage(pair, "entry")})");
        sb.AppendLine($"alertcondition(slHit,    \"Stop-loss geraakt\", {AlertMessage(pair, "stoploss")})");
        sb.AppendLine($"alertcondition(tp1Hit,   \"TP1 geraakt\",       {AlertMessage(pair, "tp1")})");
        return sb.ToString();
    }

    // ── Meerdere setups in één script ───────────────────────────────────────

    /// <summary>
    /// Eén script voor meerdere setups: tekent de niveaus van de setup die bij het grafieksymbool hoort
    /// (vergelijking op het paar, dus ook op een andere beurs met hetzelfde paar). Ongeldige setups en dubbele
    /// paren (eerste wint) worden overgeslagen; maximaal <see cref="MaxWatchlistSetups"/>.
    /// </summary>
    public static string ForWatchlist(IEnumerable<PineSetup> setups, string title, DateTime? generatedAt = null)
    {
        var list = setups
            .Where(s => s.IsValid)
            .GroupBy(s => TradingViewSymbol.PairOf(s.Ticker), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(MaxWatchlistSetups)
            .ToList();
        if (list.Count == 0) throw new ArgumentException("Geen geldige setups om te exporteren.", nameof(setups));

        var safeTitle = Safe(title);
        var sb = new StringBuilder();
        Header(sb, $"{safeTitle} — {list.Count} setups", generatedAt, list[0].Source,
            "Toont op elke grafiek automatisch de setup van dat symbool (als die in dit script zit).",
            "Symbolen: " + string.Join(", ", list.Select(s => TradingViewSymbol.PairOf(s.Ticker))),
            "Gebruik: Pine Editor → plakken → Opslaan → Toevoegen aan grafiek. Blader daarna door je watchlist.");
        sb.AppendLine($"indicator(\"CPT · {safeTitle}\", overlay = true, max_labels_count = 20)");
        sb.AppendLine();
        sb.AppendLine("float  entry = na");
        sb.AppendLine("float  sl    = na");
        sb.AppendLine("float  tp1   = na");
        sb.AppendLine("float  tp2   = na");
        sb.AppendLine("string dir   = \"\"");
        sb.AppendLine("string info  = \"\"");
        sb.AppendLine();
        sb.AppendLine("switch syminfo.ticker");
        foreach (var s in list)
        {
            sb.AppendLine($"    \"{TradingViewSymbol.PairOf(s.Ticker)}\" =>");
            sb.AppendLine($"        entry := {P(s.Entry)}");
            sb.AppendLine($"        sl    := {P(s.StopLoss)}");
            sb.AppendLine($"        tp1   := {P(s.Target1)}");
            if (s.Target2 > 0) sb.AppendLine($"        tp2   := {P(s.Target2)}");
            sb.AppendLine($"        dir   := \"{(s.IsLong ? "Long" : "Short")}\"");
            sb.AppendLine($"        info  := \"{Safe(s.Name)} · {(s.IsLong ? "Long" : "Short")}{(s.Score > 0 ? $" · score {s.Score}" : "")}\"");
        }
        sb.AppendLine();
        sb.AppendLine("isLong = dir == \"Long\"");
        sb.AppendLine("pEntry = plot(entry, \"Entry\",     color.new(color.blue, 0),  2, plot.style_linebr)");
        sb.AppendLine("pSl    = plot(sl,    \"Stop-loss\", color.new(color.red, 0),   2, plot.style_linebr)");
        sb.AppendLine("pTp1   = plot(tp1,   \"TP1\",       color.new(color.green, 0), 2, plot.style_linebr)");
        sb.AppendLine("plot(tp2, \"TP2\", color.new(color.green, 30), 1, plot.style_linebr)");
        sb.AppendLine("fill(pEntry, pSl,  color.new(color.red, 88),   \"Risicozone\")");
        sb.AppendLine("fill(pEntry, pTp1, color.new(color.green, 88), \"Winstzone\")");
        sb.AppendLine();
        sb.AppendLine("if barstate.islastconfirmedhistory and not na(entry)");
        sb.AppendLine("    label.new(bar_index + 3, entry, info + \"\\nEntry \" + str.tostring(entry, format.mintick), style = label.style_label_left, color = color.blue,  textcolor = color.white, size = size.small)");
        sb.AppendLine("    label.new(bar_index + 3, sl,    \"SL \"  + str.tostring(sl,  format.mintick), style = label.style_label_left, color = color.red,   textcolor = color.white, size = size.small)");
        sb.AppendLine("    label.new(bar_index + 3, tp1,   \"TP1 \" + str.tostring(tp1, format.mintick), style = label.style_label_left, color = color.green, textcolor = color.white, size = size.small)");
        sb.AppendLine();
        sb.AppendLine("// ── Alerts (per grafiek aan te maken; werken alleen op symbolen met een setup) ──");
        sb.AppendLine("entryHit = not na(entry) and low <= entry and high >= entry");
        sb.AppendLine("slHit    = not na(sl)  and (isLong ? low <= sl  : high >= sl)");
        sb.AppendLine("tp1Hit   = not na(tp1) and (isLong ? high >= tp1 : low <= tp1)");
        sb.AppendLine($"alertcondition(entryHit, \"Entry geraakt\",     {AlertMessage(null, "entry")})");
        sb.AppendLine($"alertcondition(slHit,    \"Stop-loss geraakt\", {AlertMessage(null, "stoploss")})");
        sb.AppendLine($"alertcondition(tp1Hit,   \"TP1 geraakt\",       {AlertMessage(null, "tp1")})");
        return sb.ToString();
    }

    /// <summary>Bestandsnaam zonder vreemde tekens, bijv. "CPT_LUNCUSDT_Short_20261003-1405.pine".</summary>
    public static string FileName(string label, DateTime when)
        => $"CPT_{Regex.Replace(label, "[^A-Za-z0-9_-]", "_")}_{when:yyyyMMdd-HHmm}.pine";

    /// <summary>Korte stappenuitleg voor het Pine Script-venster.</summary>
    public const string HowToUse =
        "1. Klik 'Kopiëren' (of 'Opslaan' voor een .pine-bestand).\n" +
        "2. Open TradingView (knop 'Open grafiek') en klik onderaan op 'Pine Editor'.\n" +
        "3. Vervang de inhoud door het gekopieerde script en klik 'Opslaan' en daarna 'Toevoegen aan grafiek'.\n" +
        "4. Alerts: klik op de grafiek op ⋯ (of Alt+A) → Alert toevoegen → Conditie: dit script → kies " +
        "'Entry geraakt', 'Stop-loss geraakt' of 'TP1 geraakt'.\n\n" +
        "Het script tekent alleen niveaus en geeft alerts — het plaatst geen orders. Geen financieel advies.";

    // ── Hulpfuncties ────────────────────────────────────────────────────────

    private static void Header(StringBuilder sb, string what, DateTime? at, string source, params string[] lines)
    {
        sb.AppendLine($"//@version={PineVersion}");
        sb.AppendLine($"// Crypto Portfolio Tracker Plus — {what}");
        sb.AppendLine($"// Gegenereerd {(at ?? DateTime.Now):dd-MM-yyyy HH:mm}{(string.IsNullOrWhiteSpace(source) ? "" : $" vanuit {Safe(source)}")}.");
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            sb.AppendLine("// " + Safe(line));
        sb.AppendLine("// De niveaus zijn een momentopname; geen financieel advies.");
    }

    private static string AlertMessage(string? pair, string evt)
        => "'{\"bron\":\"CPT\",\"event\":\"" + evt + "\",\"ticker\":\"{{ticker}}\",\"prijs\":{{close}}" +
           (pair is null ? "" : ",\"paar\":\"" + pair + "\"") + "}'";

    private static double RiskReward(PineSetup s)
    {
        double risk = Math.Abs(s.Entry - s.StopLoss);
        return risk > 0 ? Math.Abs(s.Target1 - s.Entry) / risk : 0;
    }

    /// <summary>Prijs als Pine-literal: invariant, zonder exponent, tot 16 decimalen (ook voor heel kleine prijzen).</summary>
    public static string P(double v) => v.ToString("0.################", CultureInfo.InvariantCulture);

    private static string F(double v, int decimals) => Math.Round(v, decimals).ToString(CultureInfo.InvariantCulture);

    /// <summary>Maakt tekst veilig voor een Pine-string of -commentaar (geen aanhalingstekens, backslashes of regeleinden).</summary>
    public static string Safe(string? text)
        => Regex.Replace((text ?? string.Empty).Replace('"', '\'').Replace("\\", "/"), @"[\r\n\t]+", " ").Trim();
}
