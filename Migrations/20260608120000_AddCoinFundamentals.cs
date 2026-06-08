using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    // Documenteert het CoinFundamentals-schema. De daadwerkelijke aanmaak gebeurt
    // idempotent via PortfolioService.ApplyPlusSchemaAsync (CREATE TABLE IF NOT EXISTS),
    // consistent met de overige PLUS-features.
    public partial class AddCoinFundamentals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoinFundamentals",
                columns: table => new
                {
                    Id                  = table.Column<int>     (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ApiId               = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 120),
                    Symbol              = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 40),
                    Name                = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 120),
                    Categories          = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 1000),
                    GenesisDate         = table.Column<DateTime>(type: "TEXT",    nullable: true),
                    HomepageUrl         = table.Column<string>  (type: "TEXT",    nullable: false),
                    WhitepaperUrl       = table.Column<string>  (type: "TEXT",    nullable: false),
                    GithubUrl           = table.Column<string>  (type: "TEXT",    nullable: false),
                    TwitterHandle       = table.Column<string>  (type: "TEXT",    nullable: false),
                    SubredditUrl        = table.Column<string>  (type: "TEXT",    nullable: false),
                    Description         = table.Column<string>  (type: "TEXT",    nullable: false),
                    MarketCapRank       = table.Column<long>    (type: "INTEGER", nullable: true),
                    MarketCap           = table.Column<double>  (type: "REAL",    nullable: false),
                    Fdv                 = table.Column<double>  (type: "REAL",    nullable: false),
                    TotalVolume         = table.Column<double>  (type: "REAL",    nullable: false),
                    Ath                 = table.Column<double>  (type: "REAL",    nullable: false),
                    AthChangePct        = table.Column<double>  (type: "REAL",    nullable: false),
                    AthDate             = table.Column<DateTime>(type: "TEXT",    nullable: true),
                    Atl                 = table.Column<double>  (type: "REAL",    nullable: false),
                    AtlChangePct        = table.Column<double>  (type: "REAL",    nullable: false),
                    AtlDate             = table.Column<DateTime>(type: "TEXT",    nullable: true),
                    CirculatingSupply   = table.Column<double>  (type: "REAL",    nullable: false),
                    TotalSupply         = table.Column<double>  (type: "REAL",    nullable: false),
                    MaxSupply           = table.Column<double>  (type: "REAL",    nullable: false),
                    GithubStars         = table.Column<long>    (type: "INTEGER", nullable: false),
                    GithubForks         = table.Column<long>    (type: "INTEGER", nullable: false),
                    GithubSubscribers   = table.Column<long>    (type: "INTEGER", nullable: false),
                    CommitCount4Weeks   = table.Column<long>    (type: "INTEGER", nullable: false),
                    PullRequestsMerged  = table.Column<long>    (type: "INTEGER", nullable: false),
                    PullRequestContribs = table.Column<long>    (type: "INTEGER", nullable: false),
                    TwitterFollowers    = table.Column<long>    (type: "INTEGER", nullable: false),
                    RedditSubscribers   = table.Column<long>    (type: "INTEGER", nullable: false),
                    RedditActive48H     = table.Column<double>  (type: "REAL",    nullable: false),
                    SentimentUpPct      = table.Column<double>  (type: "REAL",    nullable: false),
                    ScoreTokenomics     = table.Column<double>  (type: "REAL",    nullable: false),
                    ScoreLiquidity      = table.Column<double>  (type: "REAL",    nullable: false),
                    ScoreValuation      = table.Column<double>  (type: "REAL",    nullable: false),
                    ScoreCommunity      = table.Column<double>  (type: "REAL",    nullable: false),
                    ScoreDevelopment    = table.Column<double>  (type: "REAL",    nullable: false),
                    ScoreProject        = table.Column<double>  (type: "REAL",    nullable: false),
                    DataScore           = table.Column<double>  (type: "REAL",    nullable: false),
                    DdTeam              = table.Column<int>     (type: "INTEGER", nullable: true),
                    DdProductMaturity   = table.Column<int>     (type: "INTEGER", nullable: true),
                    DdAdoption          = table.Column<int>     (type: "INTEGER", nullable: true),
                    DdRevenue           = table.Column<int>     (type: "INTEGER", nullable: true),
                    DdUnlocks           = table.Column<int>     (type: "INTEGER", nullable: true),
                    DdNotes             = table.Column<string>  (type: "TEXT",    nullable: false),
                    TotalScore          = table.Column<double>  (type: "REAL",    nullable: false),
                    Verdict             = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 60),
                    Confidence          = table.Column<double>  (type: "REAL",    nullable: false),
                    UpdatedAt           = table.Column<DateTime>(type: "TEXT",    nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_CoinFundamentals", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_CoinFundamentals_ApiId",
                table: "CoinFundamentals",
                column: "ApiId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CoinFundamentals");
        }
    }
}
