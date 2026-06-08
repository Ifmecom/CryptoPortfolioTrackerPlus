using System;

namespace CryptoPortfolioTracker.Models;

/// <summary>
/// Persistente fundamentele analyse per coin. Combineert automatisch opgehaalde
/// CoinGecko-cijfers (supply, FDV, volume, ATH/ATL, dev- en community-data) met
/// berekende auto-subscores en handmatige due-diligence-invoer (Sprint C).
/// Eén rij per coin, gekoppeld via <see cref="ApiId"/>.
/// </summary>
public class CoinFundamentals
{
    public int Id { get; set; }

    // ── Identificatie ─────────────────────────────────────────────────────────
    public string ApiId  { get; set; } = string.Empty;   // CoinGecko id (uniek)
    public string Symbol { get; set; } = string.Empty;
    public string Name   { get; set; } = string.Empty;

    // ── Project overview ──────────────────────────────────────────────────────
    public string  Categories    { get; set; } = string.Empty;   // CSV
    public DateTime? GenesisDate  { get; set; }
    public string  HomepageUrl   { get; set; } = string.Empty;
    public string  WhitepaperUrl { get; set; } = string.Empty;
    public string  GithubUrl     { get; set; } = string.Empty;
    public string  TwitterHandle { get; set; } = string.Empty;
    public string  SubredditUrl  { get; set; } = string.Empty;
    public string  Description   { get; set; } = string.Empty;

    // ── Waardering & extremen ─────────────────────────────────────────────────
    public long?   MarketCapRank { get; set; }
    public double  MarketCap     { get; set; }
    public double  Fdv           { get; set; }
    public double  TotalVolume   { get; set; }
    public double  Ath           { get; set; }
    public double  AthChangePct  { get; set; }
    public DateTime? AthDate     { get; set; }
    public double  Atl           { get; set; }
    public double  AtlChangePct  { get; set; }
    public DateTime? AtlDate     { get; set; }

    // ── Aanbod & verwatering ──────────────────────────────────────────────────
    public double  CirculatingSupply { get; set; }
    public double  TotalSupply       { get; set; }
    public double  MaxSupply         { get; set; }

    // ── Development (GitHub) ──────────────────────────────────────────────────
    public long GithubStars         { get; set; }
    public long GithubForks         { get; set; }
    public long GithubSubscribers   { get; set; }
    public long CommitCount4Weeks   { get; set; }
    public long PullRequestsMerged  { get; set; }
    public long PullRequestContribs { get; set; }

    // ── Community ─────────────────────────────────────────────────────────────
    public long   TwitterFollowers  { get; set; }
    public long   RedditSubscribers { get; set; }
    public double RedditActive48H   { get; set; }
    public double SentimentUpPct    { get; set; }

    // ── Auto-subscores (0-100) ────────────────────────────────────────────────
    public double ScoreTokenomics  { get; set; }
    public double ScoreLiquidity   { get; set; }
    public double ScoreValuation   { get; set; }
    public double ScoreCommunity   { get; set; }
    public double ScoreDevelopment { get; set; }
    public double ScoreProject     { get; set; }

    /// <summary>Samengestelde auto-score (0-100) over de meetbare categorieën.</summary>
    public double DataScore { get; set; }

    // ── Handmatige due-diligence (0-10, null = nog niet beoordeeld) ────────────
    public int? DdTeam            { get; set; }
    public int? DdProductMaturity { get; set; }
    public int? DdAdoption        { get; set; }
    public int? DdRevenue         { get; set; }
    public int? DdUnlocks         { get; set; }
    public string DdNotes         { get; set; } = string.Empty;

    /// <summary>Volledige score (0-100) incl. handmatige DD; gelijk aan DataScore zolang DD leeg is.</summary>
    public double TotalScore { get; set; }

    /// <summary>Verdict-classificatie afgeleid van TotalScore (Exceptional … Avoid).</summary>
    public string Verdict { get; set; } = string.Empty;

    /// <summary>Betrouwbaarheid 0-100: hoeveel van het raamwerk daadwerkelijk is ingevuld.</summary>
    public double Confidence { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.MinValue;
}
