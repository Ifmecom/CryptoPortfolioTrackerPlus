using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CryptoPortfolioTracker.Infrastructure.Response.Coins;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public class CoinFullDataById
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("image")]
    public Image Image { get; set; }

    [JsonProperty("description")]
    public Dictionary<string, string> Description { get; set; }

    [JsonProperty("market_cap_rank")]
    public long? MarketCapRank { get; set; }

    // ── Fundamentals (project overview) ──────────────────────────────────────
    [JsonProperty("categories")]
    public List<string> Categories { get; set; }

    [JsonProperty("genesis_date")]
    public string GenesisDate { get; set; }

    [JsonProperty("sentiment_votes_up_percentage")]
    public double? SentimentVotesUpPercentage { get; set; }

    [JsonProperty("links")]
    public Links Links { get; set; }

    [JsonProperty("developer_data")]
    public DeveloperData DeveloperData { get; set; }

    [JsonProperty("community_data")]
    public CommunityData CommunityData { get; set; }

    [JsonProperty("market_data", NullValueHandling = NullValueHandling.Ignore)]
    public MarketData MarketData { get; set; }
}

public class MarketData
{
    [JsonProperty("market_cap_rank")]
    public long? MarketCapRank { get; set; }

    [JsonProperty("ath")]
    public Dictionary<string, double?> Ath { get; set; }

    [JsonProperty("current_price")]
    public Dictionary<string, double?> CurrentPrice { get; set; }

    [JsonProperty("market_cap")]
    public Dictionary<string, double?> MarketCap { get; set; }

    // ── Fundamentals (tokenomics, liquidity, valuation extremes) ─────────────
    [JsonProperty("fully_diluted_valuation")]
    public Dictionary<string, double?> FullyDilutedValuation { get; set; }

    [JsonProperty("total_volume")]
    public Dictionary<string, double?> TotalVolume { get; set; }

    [JsonProperty("circulating_supply")]
    public double? CirculatingSupply { get; set; }

    [JsonProperty("total_supply")]
    public double? TotalSupply { get; set; }

    [JsonProperty("max_supply")]
    public double? MaxSupply { get; set; }

    [JsonProperty("atl")]
    public Dictionary<string, double?> Atl { get; set; }

    [JsonProperty("ath_change_percentage")]
    public Dictionary<string, double?> AthChangePercentage { get; set; }

    [JsonProperty("atl_change_percentage")]
    public Dictionary<string, double?> AtlChangePercentage { get; set; }

    [JsonProperty("ath_date")]
    public Dictionary<string, DateTime?> AthDate { get; set; }

    [JsonProperty("atl_date")]
    public Dictionary<string, DateTime?> AtlDate { get; set; }

    [JsonProperty("price_change_percentage_24h_in_currency")]
    public Dictionary<string, double> PriceChangePercentage24HInCurrency { get; set; }

    [JsonProperty("price_change_percentage_30d_in_currency")]
    public Dictionary<string, double> PriceChangePercentage30DInCurrency { get; set; }

    [JsonProperty("price_change_percentage_1y_in_currency")]
    public Dictionary<string, double> PriceChangePercentage1YInCurrency { get; set; }
}

public class Links
{
    [JsonProperty("homepage")]
    public List<string> Homepage { get; set; }

    [JsonProperty("whitepaper")]
    public string Whitepaper { get; set; }

    [JsonProperty("repos_url")]
    public ReposUrl ReposUrl { get; set; }

    [JsonProperty("subreddit_url")]
    public string SubredditUrl { get; set; }

    [JsonProperty("twitter_screen_name")]
    public string TwitterScreenName { get; set; }
}

public class ReposUrl
{
    [JsonProperty("github")]
    public List<string> Github { get; set; }
}

public class DeveloperData
{
    [JsonProperty("forks")]
    public long? Forks { get; set; }

    [JsonProperty("stars")]
    public long? Stars { get; set; }

    [JsonProperty("subscribers")]
    public long? Subscribers { get; set; }

    [JsonProperty("total_issues")]
    public long? TotalIssues { get; set; }

    [JsonProperty("closed_issues")]
    public long? ClosedIssues { get; set; }

    [JsonProperty("pull_requests_merged")]
    public long? PullRequestsMerged { get; set; }

    [JsonProperty("pull_request_contributors")]
    public long? PullRequestContributors { get; set; }

    [JsonProperty("commit_count_4_weeks")]
    public long? CommitCount4Weeks { get; set; }
}

public class CommunityData
{
    [JsonProperty("twitter_followers")]
    public long? TwitterFollowers { get; set; }

    [JsonProperty("reddit_subscribers")]
    public long? RedditSubscribers { get; set; }

    [JsonProperty("reddit_average_posts_48h")]
    public double? RedditAveragePosts48H { get; set; }

    [JsonProperty("reddit_accounts_active_48h")]
    public double? RedditAccountsActive48H { get; set; }
}

public class Image
{
    [JsonProperty("thumb")]
    public Uri Thumb { get; set; }

    [JsonProperty("small")]
    public Uri Small { get; set; }

    [JsonProperty("large")]
    public Uri Large { get; set; }
}

#pragma warning restore CS8618
