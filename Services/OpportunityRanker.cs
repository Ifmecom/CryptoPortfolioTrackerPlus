using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CryptoPortfolioTracker.Services;

/// <summary>Eén setup/advies zoals een pagina het aan de <see cref="OpportunityRanker"/> doorgeeft.</summary>
/// <param name="Key">Unieke sleutel binnen de lijst (bijv. symbool of id).</param>
/// <param name="Direction">"Long" / "Short"; iets anders telt niet mee.</param>
/// <param name="Quality">De eigen kwaliteitsscore van de pagina, 0–100 (sterker = hoger).</param>
/// <param name="RiskReward">Verhouding winst/risico naar het eerste doel (2 = 1:2), of null als onbekend.</param>
/// <param name="HitRate">Gemeten trefkans 0–1 voor vergelijkbare setups, of null als onbekend.</param>
/// <param name="HitRateSamples">Aantal metingen achter <paramref name="HitRate"/> (voor de uitleg).</param>
/// <param name="HitRateReliable">Genoeg metingen volgens de drempel van de bron (zie ReliabilityThresholds).</param>
/// <param name="Eligible">False = ongeldige of geweigerde setup; doet niet mee.</param>
public sealed record OpportunityInput(
    string  Key,
    string  Direction,
    double  Quality,
    double? RiskReward      = null,
    double? HitRate         = null,
    int     HitRateSamples  = 0,
    bool    HitRateReliable = false,
    bool    CounterTrend    = false,
    bool    TfConflict      = false,
    bool    ThinLiquidity   = false,
    bool    NearBreakout    = false,
    bool    Eligible        = true);

/// <summary>Uitkomst per setup: positie (1 = beste), kansscore en een uitleg van de opbouw.</summary>
public sealed record OpportunityRank(string Key, int Rank, double KansScore, string Explanation);

/// <summary>
/// Bepaalt welke setups het eerst het beoordelen waard zijn (v1.48, "Top X"). Puur en getest; dezelfde formule
/// op elke pagina met setups of adviezen, zodat "top 5" overal hetzelfde betekent.
/// <para>
/// Kansscore = kwaliteit × R/R-factor × bewijsfactor × waarschuwingsfactoren, begrensd op 0–100:
/// <list type="bullet">
/// <item>R/R-factor: R/R ÷ 2, begrensd 0,25–1,4 (1:2 is neutraal; 1:1 = ×0,5; onbekend = 1). Een slechte R/R moet echt
///   wegzakken: 1:0,2 riskeert 5× de winst.</item>
/// <item>Bewijsfactor (alleen bij een betrouwbare gemeten trefkans p): verwachting E = p·R/R − (1−p) in R
///   (zonder R/R wordt 1:1 aangenomen), factor = 1 + E/2, begrensd 0,6–1,4.</item>
/// <item>Tegen de daily-trend ×0,85 · TF-conflict ×0,90 · dunne liquiditeit ×0,80 · bijna breakout ×1,05.</item>
/// </list>
/// Het is een volgorde om te beoordelen, geen voorspelling of advies.
/// </para>
/// </summary>
public static class OpportunityRanker
{
    /// <summary>Keuzes in de Top-keuzelijst; 0 = uit.</summary>
    public static readonly IReadOnlyList<int> TopOptions = new[] { 0, 3, 5, 10, 20 };

    /// <summary>Ondergrens R/R-factor: 1:0,5 of slechter → ×0,25.</summary>
    public const double MinRiskRewardFactor = 0.25;

    public const double CounterTrendFactor  = 0.85;
    public const double TfConflictFactor    = 0.90;
    public const double ThinLiquidityFactor = 0.80;
    public const double NearBreakoutFactor  = 1.05;

    private static readonly CultureInfo Nl = CultureInfo.GetCultureInfo("nl-NL");

    public static bool IsEligible(OpportunityInput i)
        => i.Eligible
           && (i.Direction == "Long" || i.Direction == "Short")
           && i.Quality > 0 && !double.IsNaN(i.Quality);

    /// <summary>Kansscore (0–100) en de opbouw in leesbare regels.</summary>
    public static (double Score, IReadOnlyList<string> Parts) Score(OpportunityInput i)
    {
        var parts = new List<string>();
        double quality = Math.Clamp(i.Quality, 0, 100);
        parts.Add($"Kwaliteitsscore {F(quality, "0")}");
        double score = quality;

        bool rrKnown = i.RiskReward is > 0 && !double.IsNaN(i.RiskReward.Value);
        if (rrKnown)
        {
            double fRr = Math.Clamp(i.RiskReward!.Value / 2.0, MinRiskRewardFactor, 1.4);
            score *= fRr;
            parts.Add($"R/R 1:{F(i.RiskReward.Value, "0.0")} → ×{F(fRr, "0.00")}");
        }
        else
        {
            parts.Add("R/R onbekend → ×1,00");
        }

        if (i.HitRate is { } p && !double.IsNaN(p))
        {
            p = Math.Clamp(p, 0, 1);
            if (i.HitRateReliable)
            {
                double rr = rrKnown ? i.RiskReward!.Value : 1.0;
                double e  = p * rr - (1 - p);
                double fE = Math.Clamp(1 + e / 2.0, 0.6, 1.4);
                score *= fE;
                parts.Add($"Gemeten trefkans {F(p * 100, "0")}% (n={i.HitRateSamples}) → verwachting " +
                          $"{(e >= 0 ? "+" : "")}{F(e, "0.00")}R{(rrKnown ? "" : " bij 1:1")} → ×{F(fE, "0.00")}");
            }
            else
            {
                parts.Add($"Gemeten trefkans {F(p * 100, "0")}% telt niet mee (n={i.HitRateSamples}, te weinig metingen)");
            }
        }

        if (i.CounterTrend)  { score *= CounterTrendFactor;  parts.Add($"Tegen de daily-trend → ×{F(CounterTrendFactor, "0.00")}"); }
        if (i.TfConflict)    { score *= TfConflictFactor;    parts.Add($"TF-conflict 1D/4H → ×{F(TfConflictFactor, "0.00")}"); }
        if (i.ThinLiquidity) { score *= ThinLiquidityFactor; parts.Add($"Dunne liquiditeit → ×{F(ThinLiquidityFactor, "0.00")}"); }
        if (i.NearBreakout)  { score *= NearBreakoutFactor;  parts.Add($"Bijna breakout → ×{F(NearBreakoutFactor, "0.00")}"); }

        return (Math.Round(Math.Clamp(score, 0, 100), 1), parts);
    }

    /// <summary>
    /// Rangschikt de geschikte setups (beste eerst). Gelijke kansscore: hogere kwaliteit, dan hogere R/R, dan sleutel.
    /// Ongeschikte setups komen niet in de uitkomst.
    /// </summary>
    public static IReadOnlyList<OpportunityRank> Rank(IEnumerable<OpportunityInput> inputs)
    {
        var scored = inputs
            .Where(IsEligible)
            .Select(i => (Input: i, Result: Score(i)))
            .OrderByDescending(x => x.Result.Score)
            .ThenByDescending(x => x.Input.Quality)
            .ThenByDescending(x => x.Input.RiskReward ?? 0)
            .ThenBy(x => x.Input.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return scored
            .Select((x, idx) => new OpportunityRank(
                x.Input.Key, idx + 1, x.Result.Score,
                $"🏆 #{idx + 1} — kansscore {F(x.Result.Score, "0.0")}\n" + string.Join("\n", x.Result.Parts.Select(s => "• " + s))))
            .ToList();
    }

    /// <summary>
    /// Zet rang, kansscore en uitleg op de rijen. <paramref name="keyOf"/> moet dezelfde sleutel geven als
    /// <paramref name="toInput"/>. Rijen die niet meedoen krijgen rang 0.
    /// </summary>
    public static void Apply<T>(IEnumerable<T> rows, Func<T, OpportunityInput> toInput, int topCount)
        where T : ITopPickRow
    {
        var list   = rows.ToList();
        var inputs = list.Select(r => (Row: r, Input: toInput(r))).ToList();
        // Dubbele sleutels (zou niet mogen) mogen nooit crashen: de beste rang wint.
        var ranks  = Rank(inputs.Select(x => x.Input))
            .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (row, input) in inputs)
        {
            if (ranks.TryGetValue(input.Key, out var r))
            {
                row.TopRank        = r.Rank;
                row.KansScore      = r.KansScore;
                row.TopExplanation = r.Explanation;
                row.IsTopPick      = topCount > 0 && r.Rank <= topCount;
            }
            else
            {
                row.TopRank = 0; row.KansScore = 0; row.TopExplanation = string.Empty; row.IsTopPick = false;
            }
        }
    }

    /// <summary>
    /// De rijen om te tonen: alles (in de bestaande volgorde) of — bij "alleen top" — de top X op rang.
    /// </summary>
    public static List<T> Visible<T>(IEnumerable<T> rows, int topCount, bool onlyTop) where T : ITopPickRow
        => onlyTop && topCount > 0
            ? rows.Where(r => r.IsTopPick).OrderBy(r => r.TopRank).ToList()
            : rows.ToList();

    /// <summary>Uitleg voor de ?-knop naast de Top-keuze.</summary>
    public const string HowItWorks =
        "De top X bepaalt welke setups het eerst het beoordelen waard zijn — binnen de lijst die je nu ziet " +
        "(na je filters). Elke setup krijgt een kansscore (0–100):\n\n" +
        "• Basis: de eigen kwaliteitsscore van deze pagina.\n" +
        "• × R/R: winst/risico naar het eerste doel; 1:2 is neutraal, 1:1 halveert, 1:0,5 of slechter ×0,25, 1:2,8 of beter ×1,4.\n" +
        "• × bewijs: alleen als er genoeg metingen zijn van vergelijkbare setups — de gemeten trefkans wordt " +
        "omgerekend naar een verwachte opbrengst in R (×0,6–×1,4).\n" +
        "• × waarschuwingen: tegen de daily-trend ×0,85, TF-conflict ×0,90, dunne liquiditeit ×0,80; " +
        "bijna breakout ×1,05.\n\n" +
        "Setups zonder richting of met een ongeldige setup doen niet mee. Beweeg over 🏆 voor de opbouw per " +
        "setup. Dit is een volgorde om te beoordelen, geen voorspelling of financieel advies.";

    private static string F(double v, string fmt) => v.ToString(fmt, Nl);
}

/// <summary>Een rij die een Top X-rang kan dragen (v1.48).</summary>
public interface ITopPickRow
{
    int    TopRank        { get; set; }
    double KansScore      { get; set; }
    string TopExplanation { get; set; }
    bool   IsTopPick      { get; set; }
}
