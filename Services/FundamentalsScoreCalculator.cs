using System;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Pure, testbare berekening van de fundamentele auto-subscores (0-100) uit
/// CoinGecko-cijfers, plus de samengestelde data-score en — wanneer handmatige
/// due-diligence is ingevuld — de volledige score met verdict.
///
/// Alle drempels zijn bewust transparant en gedocumenteerd zodat de gebruiker
/// begrijpt waarom een coin een bepaalde score krijgt. De gewichten gelden voor
/// de meetbare categorieën; niet-meetbare categorieën (team, maturiteit, adoptie,
/// revenue, unlocks) komen via handmatige DD binnen.
/// </summary>
public static class FundamentalsScoreCalculator
{
    // Gewichten van de auto-subscores binnen de data-score (scommeren tot 1.0)
    public const double WTokenomics  = 0.25;
    public const double WLiquidity   = 0.20;
    public const double WValuation   = 0.15;
    public const double WCommunity   = 0.15;
    public const double WDevelopment = 0.15;
    public const double WProject     = 0.10;

    // Aandeel van de auto-data-score in de volledige score wanneer alle 5 DD-velden
    // zijn ingevuld. (DD telt dan voor 1 - DataWeightWhenFullyAssessed.)
    public const double DataWeightWhenFullyAssessed = 0.55;
    private const int DdFieldCount = 5;

    private static double Clamp(double v, double lo = 0, double hi = 100) => Math.Max(lo, Math.Min(hi, v));

    /// <summary>Lineaire interpolatie van x∈[x0,x1] naar [y0,y1], geclampt.</summary>
    private static double Lerp(double x, double x0, double x1, double y0, double y1)
    {
        if (x1 == x0) return y0;
        double t = (x - x0) / (x1 - x0);
        t = Math.Max(0, Math.Min(1, t));
        return y0 + t * (y1 - y0);
    }

    /// <summary>Logaritmische score: bereikt 100 bij <paramref name="full"/>.</summary>
    private static double LogScore(double value, double full)
    {
        if (value <= 0) return 0;
        if (full <= 1) return 100;
        return Clamp(Math.Log10(value + 1) / Math.Log10(full + 1) * 100.0);
    }

    // ── Subscores ─────────────────────────────────────────────────────────────

    /// <summary>Aanbod & verwatering: circulerend aandeel + FDV/MC-overhang.</summary>
    public static double Tokenomics(double circulating, double total, double max, double marketCap, double fdv)
    {
        // Circulerend aandeel van het maximale aanbod (geen max → val terug op total).
        double cap = max > 0 ? max : total;
        double circScore = cap > 0 ? Clamp(circulating / cap * 100.0) : 50.0; // onbekend → neutraal

        // Verwatering: FDV/MC. ~1 = nauwelijks toekomstige inflatie; hoog = veel overhang.
        double dilutionScore;
        if (marketCap <= 0 || fdv <= 0) dilutionScore = 50.0;
        else
        {
            double ratio = fdv / marketCap;
            if (ratio <= 1.1) dilutionScore = 100;
            else if (ratio <= 2.0) dilutionScore = Lerp(ratio, 1.1, 2.0, 100, 60);
            else if (ratio <= 4.0) dilutionScore = Lerp(ratio, 2.0, 4.0, 60, 20);
            else dilutionScore = 10;
        }
        return Clamp(0.5 * circScore + 0.5 * dilutionScore);
    }

    /// <summary>Volume/MC-ratio als liquiditeitsmaat. Te hoog kan op wash-trading wijzen.</summary>
    public static double Liquidity(double totalVolume, double marketCap)
    {
        if (marketCap <= 0) return 0;
        double r = totalVolume / marketCap;
        if (r <= 0)      return 0;
        if (r < 0.02)    return Lerp(r, 0,    0.02, 15, 70);
        if (r <= 0.30)   return Lerp(r, 0.02, 0.30, 70, 100);
        if (r <= 1.0)    return Lerp(r, 0.30, 1.0, 100, 70);
        return 50; // >1 verdacht hoog
    }

    /// <summary>Waardering/gevestigdheid: vooral market-cap rank, licht bijgesteld op ATL-herstel.</summary>
    public static double Valuation(long? rank, double atlChangePct)
    {
        double rankScore;
        if (rank is null || rank <= 0) rankScore = 30;
        else if (rank <= 50)   rankScore = Lerp(rank.Value, 1, 50, 100, 90);
        else if (rank <= 200)  rankScore = Lerp(rank.Value, 50, 200, 90, 70);
        else if (rank <= 500)  rankScore = Lerp(rank.Value, 200, 500, 70, 50);
        else if (rank <= 1500) rankScore = Lerp(rank.Value, 500, 1500, 50, 30);
        else rankScore = 25;

        // Sterk herstel vanaf ATL is licht positief; net boven ATL licht negatief.
        double recovery = Clamp(Lerp(atlChangePct, 0, 1000, 40, 100), 40, 100);
        return Clamp(0.8 * rankScore + 0.2 * recovery);
    }

    /// <summary>Community: Twitter + Reddit (log-schaal) + sentiment.</summary>
    public static double Community(long twitterFollowers, long redditSubscribers, double sentimentUpPct)
    {
        double tw  = LogScore(twitterFollowers, 1_000_000);
        double rd  = LogScore(redditSubscribers, 500_000);
        double sen = Clamp(sentimentUpPct);                       // 0-100
        bool hasSen = sentimentUpPct > 0;
        return hasSen
            ? Clamp(0.45 * tw + 0.35 * rd + 0.20 * sen)
            : Clamp((0.45 * tw + 0.35 * rd) / 0.80);              // sentiment ontbreekt → herweeg
    }

    /// <summary>Development: recente commits + sterren + gemergede PR's.</summary>
    public static double Development(long commits4w, long stars, long pullRequestsMerged)
    {
        bool hasRepo = stars > 0 || commits4w > 0 || pullRequestsMerged > 0;
        if (!hasRepo) return 0; // geen (publieke) repo bekend

        double commitScore;
        if (commits4w <= 0)      commitScore = 0;
        else if (commits4w < 10) commitScore = Lerp(commits4w, 0, 10, 20, 50);
        else if (commits4w < 50) commitScore = Lerp(commits4w, 10, 50, 50, 85);
        else commitScore = Clamp(Lerp(commits4w, 50, 150, 85, 100));

        double starScore = LogScore(stars, 40_000);
        double prScore   = LogScore(pullRequestsMerged, 20_000);
        return Clamp(0.5 * commitScore + 0.3 * starScore + 0.2 * prScore);
    }

    /// <summary>Projectvolledigheid: links/whitepaper/sector/leeftijd.</summary>
    public static double Project(bool hasWhitepaper, bool hasGithub, bool hasHomepage, bool hasCategory, DateTime? genesis, DateTime now)
    {
        double s = 0;
        if (hasHomepage)   s += 15;
        if (hasWhitepaper) s += 30;
        if (hasGithub)     s += 25;
        if (hasCategory)   s += 10;
        if (genesis is { } g && g <= now)
        {
            double years = (now - g).TotalDays / 365.25;
            s += Clamp(Lerp(years, 0, 2, 0, 20), 0, 20); // tot 2 jaar track record → +20
        }
        return Clamp(s);
    }

    // ── Compositie ──────────────────────────────────────────────────────────────

    /// <summary>Samengestelde auto-data-score uit de zes subscores.</summary>
    public static double DataScore(CoinFundamentals f) => Clamp(
          WTokenomics  * f.ScoreTokenomics
        + WLiquidity   * f.ScoreLiquidity
        + WValuation   * f.ScoreValuation
        + WCommunity   * f.ScoreCommunity
        + WDevelopment * f.ScoreDevelopment
        + WProject     * f.ScoreProject);

    /// <summary>
    /// Volledige score: blend van de auto-data-score met de gemiddelde handmatige
    /// DD-score (0-10 → 0-100). Het DD-gewicht schaalt met het aantal ingevulde velden,
    /// zodat een half ingevulde beoordeling de score niet onevenredig stuurt.
    /// </summary>
    public static double TotalScore(double dataScore, CoinFundamentals f)
    {
        int filled = 0; double sum = 0;
        foreach (var dd in new[] { f.DdTeam, f.DdProductMaturity, f.DdAdoption, f.DdRevenue, f.DdUnlocks })
            if (dd is { } v) { filled++; sum += Math.Max(0, Math.Min(10, v)) * 10.0; }

        if (filled == 0) return Clamp(dataScore);

        double ddAvg       = sum / filled;
        double ddWeight    = DataWeightWhenFullyAssessed == 1 ? 0
                           : (1 - DataWeightWhenFullyAssessed) * filled / DdFieldCount;
        double dataWeight  = 1 - ddWeight;
        return Clamp(dataWeight * dataScore + ddWeight * ddAvg);
    }

    /// <summary>Hoeveel van het raamwerk daadwerkelijk is onderbouwd (0-100).</summary>
    public static double Confidence(CoinFundamentals f)
    {
        // Auto-data dekt de meetbare helft; elke ingevulde DD voegt de andere helft toe.
        int filled = 0;
        foreach (var dd in new[] { f.DdTeam, f.DdProductMaturity, f.DdAdoption, f.DdRevenue, f.DdUnlocks })
            if (dd is not null) filled++;
        return Clamp(50 + 50.0 * filled / DdFieldCount);
    }

    /// <summary>Verdict-classificatie volgens het analyse-raamwerk.</summary>
    public static string Verdict(double total) => total switch
    {
        >= 90 => "Exceptional",
        >= 80 => "Strong Investment Candidate",
        >= 70 => "Promising",
        >= 60 => "Speculative",
        >= 50 => "High Risk",
        _     => "Avoid",
    };

    /// <summary>Berekent en zet alle subscores, de samengestelde scores en het verdict op <paramref name="f"/>.</summary>
    public static void Recompute(CoinFundamentals f, DateTime now)
    {
        f.ScoreTokenomics  = Tokenomics(f.CirculatingSupply, f.TotalSupply, f.MaxSupply, f.MarketCap, f.Fdv);
        f.ScoreLiquidity   = Liquidity(f.TotalVolume, f.MarketCap);
        f.ScoreValuation   = Valuation(f.MarketCapRank, f.AtlChangePct);
        f.ScoreCommunity   = Community(f.TwitterFollowers, f.RedditSubscribers, f.SentimentUpPct);
        f.ScoreDevelopment = Development(f.CommitCount4Weeks, f.GithubStars, f.PullRequestsMerged);
        f.ScoreProject     = Project(
            hasWhitepaper: !string.IsNullOrWhiteSpace(f.WhitepaperUrl),
            hasGithub:     !string.IsNullOrWhiteSpace(f.GithubUrl),
            hasHomepage:   !string.IsNullOrWhiteSpace(f.HomepageUrl),
            hasCategory:   !string.IsNullOrWhiteSpace(f.Categories),
            genesis:       f.GenesisDate,
            now:           now);

        f.DataScore   = DataScore(f);
        f.TotalScore  = TotalScore(f.DataScore, f);
        f.Confidence  = Confidence(f);
        f.Verdict     = Verdict(f.TotalScore);
    }
}
