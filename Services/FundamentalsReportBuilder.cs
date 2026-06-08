using System.Collections.Generic;
using System.Linq;
using CryptoPortfolioTracker.Models;

namespace CryptoPortfolioTracker.Services;

/// <summary>
/// Bouwt een transparant, rule-based SWOT/risico/waarderings-rapport uit de
/// fundamentele cijfers en subscores. Volledig deterministisch en testbaar —
/// geen externe AI of verzonnen tekst.
/// </summary>
public static class FundamentalsReportBuilder
{
    public static FundamentalsReport Build(CoinFundamentals f)
    {
        var r = new FundamentalsReport();

        double fdvMc = f.MarketCap > 0 && f.Fdv > 0 ? f.Fdv / f.MarketCap : 0;
        double volMc = f.MarketCap > 0 ? f.TotalVolume / f.MarketCap : 0;
        double circPct = f.MaxSupply > 0 ? f.CirculatingSupply / f.MaxSupply * 100 : 0;
        double mcapTvl = f.Tvl > 0 && f.MarketCap > 0 ? f.MarketCap / f.Tvl : 0;
        long? rank = f.MarketCapRank;

        // ── Strengths ────────────────────────────────────────────────────────
        if (f.ScoreLiquidity >= 75) r.Strengths.Add("Goede liquiditeit — gezond volume t.o.v. market cap.");
        if (fdvMc > 0 && fdvMc < 1.3 && circPct >= 70) r.Strengths.Add("Lage verwatering: het grootste deel van het aanbod is al in omloop.");
        if (f.ScoreCommunity >= 70) r.Strengths.Add("Sterke community (Twitter/Reddit).");
        if (f.ScoreDevelopment >= 70) r.Strengths.Add("Actieve ontwikkeling — recente commits en repo-activiteit.");
        if (rank is > 0 and <= 100) r.Strengths.Add($"Gevestigde large/mid-cap (rang #{rank}).");
        if (f.ScoreProject >= 80) r.Strengths.Add("Compleet projectdossier (whitepaper, repo en track record).");
        if (f.AtlChangePct >= 300) r.Strengths.Add("Sterk hersteld vanaf de bodem (ATL).");
        if (f.Tvl >= 1_000_000_000) r.Strengths.Add("Substantiële TVL (>$1B) — reële on-chain adoptie.");
        if (mcapTvl > 0 && mcapTvl < 2) r.Strengths.Add($"Waardering goed gedekt door TVL (market cap/TVL {mcapTvl:0.0}×).");
        if (f.DdTeam is >= 8) r.Strengths.Add("Hoog beoordeeld op team/organisatie.");
        if (f.DdRevenue is >= 8) r.Strengths.Add("Hoog beoordeeld op revenue/business-model.");

        // ── Weaknesses ───────────────────────────────────────────────────────
        if (f.ScoreLiquidity < 40) r.Weaknesses.Add("Beperkte liquiditeit — lastiger in- en uitstappen.");
        if (fdvMc >= 3) r.Weaknesses.Add($"Hoge verwateringsoverhang (FDV is {fdvMc:0.0}× de market cap).");
        if (f.CommitCount4Weeks == 0) r.Weaknesses.Add("Geen recente publieke ontwikkelactiviteit (0 commits/4 wkn).");
        if (f.ScoreCommunity < 35) r.Weaknesses.Add("Kleine of zwakke community.");
        if (string.IsNullOrWhiteSpace(f.WhitepaperUrl) || string.IsNullOrWhiteSpace(f.GithubUrl))
            r.Weaknesses.Add("Geen whitepaper en/of publieke repo gekoppeld.");
        if (rank is null or > 1000) r.Weaknesses.Add("Zeer kleine of onbekende marktkapitalisatie.");
        if (f.AthChangePct <= -85) r.Weaknesses.Add($"Ver onder all-time high ({f.AthChangePct:0}%).");
        if (f.DdTeam is <= 3) r.Weaknesses.Add("Zwak beoordeeld op team/organisatie.");
        if (f.DdProductMaturity is <= 3) r.Weaknesses.Add("Product nog onvolwassen (handmatige beoordeling).");
        if (f.DdAdoption is <= 3) r.Weaknesses.Add("Lage adoptie (handmatige beoordeling).");
        if (mcapTvl > 15) r.Weaknesses.Add($"Hoge market-cap/TVL ratio ({mcapTvl:0}×) — waardering loopt ver voor op de vergrendelde waarde.");

        // ── Opportunities ──────────────────────────────────────────────────────
        if (f.AthChangePct <= -70 && f.ScoreLiquidity >= 60 && f.ScoreDevelopment >= 60)
            r.Opportunities.Add("Fors onder ATH terwijl liquiditeit en ontwikkeling intact zijn — mogelijke waarde.");
        if (f.ScoreDevelopment >= 70 && rank is > 200)
            r.Opportunities.Add("Actieve ontwikkeling bij nog relatief lage marktcap — potentieel onder de radar.");
        if (fdvMc > 0 && fdvMc < 1.3 && circPct >= 80)
            r.Opportunities.Add("Weinig toekomstige verwatering verwacht — schaarste-voordeel.");
        if (mcapTvl > 0 && mcapTvl < 1)
            r.Opportunities.Add($"Market cap onder de TVL ({mcapTvl:0.0}×) — mogelijk ondergewaardeerd t.o.v. deposits.");
        if (f.DdRevenue is >= 7)
            r.Opportunities.Add("Reëel verdienmodel — duurzamer dan puur speculatieve tokens.");

        // ── Threats ──────────────────────────────────────────────────────────
        if (fdvMc >= 3) r.Threats.Add("Toekomstige token-unlocks kunnen aanhoudende verkoopdruk geven.");
        if (f.ScoreLiquidity < 40) r.Threats.Add("Liquiditeitsrisico: grotere posities zijn moeilijk te verkopen.");
        if (volMc > 1) r.Threats.Add("Verdacht hoog volume t.o.v. market cap — mogelijk wash-trading.");
        if (f.DdUnlocks is <= 3) r.Threats.Add("Hoog unlock-/vesting-risico (handmatige beoordeling).");
        r.Threats.Add("Marktbrede en regelgevingsrisico's (niet coin-specifiek).");

        // ── Risk level ─────────────────────────────────────────────────────────
        int risk = 0;
        if (fdvMc >= 3) risk += 2;
        if (f.ScoreLiquidity < 40) risk += 2;
        if (f.CommitCount4Weeks == 0) risk += 1;
        if (rank is null or > 1000) risk += 1;
        if (f.ScoreCommunity < 35) risk += 1;
        if (f.DdUnlocks is <= 3) risk += 1;
        if (mcapTvl > 15) risk += 1;
        if (f.TotalScore < 50) risk += 1;
        r.RiskLevel = risk >= 4 ? "HIGH" : risk >= 2 ? "MEDIUM" : "LOW";

        // ── Valuation (heuristisch) ──────────────────────────────────────────────
        // Market-cap/TVL is voor DeFi-protocollen een sterker signaal dan FDV alleen.
        if (mcapTvl > 0)
            r.ValuationVerdict = mcapTvl > 15
                ? $"OVERGEWAARDEERD (heuristisch) — market cap is {mcapTvl:0}× de TVL."
                : mcapTvl < 1.5 && f.TotalScore >= 65
                    ? $"AANTREKKELIJK GEWAARDEERD (heuristisch) — market cap/TVL {mcapTvl:0.0}× met sterke fundamentals."
                    : $"REDELIJK GEWAARDEERD t.o.v. TVL (market cap/TVL {mcapTvl:0.0}×).";
        else if (fdvMc >= 4)
            r.ValuationVerdict = "OVERGEWAARDEERD (heuristisch) — een groot deel van de waardering zit in nog niet-circulerend aanbod.";
        else if (f.TotalScore >= 75 && fdvMc > 0 && fdvMc < 1.5)
            r.ValuationVerdict = "AANTREKKELIJK GEWAARDEERD (heuristisch) — sterke fundamentals met lage verwatering.";
        else
            r.ValuationVerdict = "REDELIJK / MOEILIJK TE BEPALEN — zonder revenue-/TVL-data is een harde waardering niet mogelijk.";

        // ── Top risks (max 5, gecombineerd uit threats + weaknesses) ──────────────
        r.TopRisks.AddRange(r.Threats.Concat(r.Weaknesses).Distinct().Take(5));

        // ── Executive summary ──────────────────────────────────────────────────
        var (bestName, _) = BestFactor(f, true);
        var (worstName, _) = BestFactor(f, false);
        r.ExecutiveSummary =
            $"{f.Name} scoort {f.TotalScore:0}/100 ({f.Verdict}). " +
            $"Sterkste factor: {bestName}; zwakste: {worstName}. " +
            $"Risiconiveau: {r.RiskLevel}. {r.ValuationVerdict.Split('—')[0].Trim()}.";

        return r;
    }

    private static (string name, double score) BestFactor(CoinFundamentals f, bool highest)
    {
        var factors = new (string, double)[]
        {
            ("Tokenomics", f.ScoreTokenomics),
            ("Liquiditeit", f.ScoreLiquidity),
            ("Waardering", f.ScoreValuation),
            ("Community", f.ScoreCommunity),
            ("Development", f.ScoreDevelopment),
            ("Projectvolledigheid", f.ScoreProject),
        };
        return highest
            ? factors.OrderByDescending(x => x.Item2).First()
            : factors.OrderBy(x => x.Item2).First();
    }
}
