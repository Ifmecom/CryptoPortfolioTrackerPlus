using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    public partial class AddPlusFeatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // New columns on Coins
            migrationBuilder.AddColumn<double>("Macd",          "Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("MacdSignal",    "Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("BollingerUpper","Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("BollingerLower","Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("Atr",           "Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("StochRsi",      "Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("LatestSentimentScore", "Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("LatestSignalScore",    "Coins", "REAL", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<string>("MarketRegime",  "Coins", "TEXT", nullable: false, defaultValue: "Neutral");

            // BronSources
            migrationBuilder.CreateTable(
                name: "BronSources",
                columns: table => new
                {
                    Id               = table.Column<int>   (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Type             = table.Column<string>(type: "TEXT",    nullable: false),
                    Url              = table.Column<string>(type: "TEXT",    nullable: false, maxLength: 500),
                    Handle           = table.Column<string>(type: "TEXT",    nullable: false, maxLength: 200),
                    ReliabilityScore = table.Column<double>(type: "REAL",    nullable: false),
                    IsActive         = table.Column<bool>  (type: "INTEGER", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_BronSources", x => x.Id));

            // ExchangeAccounts
            migrationBuilder.CreateTable(
                name: "ExchangeAccounts",
                columns: table => new
                {
                    Id                = table.Column<int>   (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Exchange          = table.Column<string>(type: "TEXT",    nullable: false),
                    ApiKeyEncrypted   = table.Column<string>(type: "TEXT",    nullable: false),
                    ApiSecretEncrypted= table.Column<string>(type: "TEXT",    nullable: false),
                    Permissions       = table.Column<string>(type: "TEXT",    nullable: false, maxLength: 500),
                    IsActive          = table.Column<bool>  (type: "INTEGER", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ExchangeAccounts", x => x.Id));

            // SentimentReadings
            migrationBuilder.CreateTable(
                name: "SentimentReadings",
                columns: table => new
                {
                    Id             = table.Column<int>     (type: "INTEGER",  nullable: false).Annotation("Sqlite:Autoincrement", true),
                    CoinId         = table.Column<int>     (type: "INTEGER",  nullable: false),
                    Source         = table.Column<string>  (type: "TEXT",     nullable: false),
                    SentimentScore = table.Column<double>  (type: "REAL",     nullable: false),
                    Confidence     = table.Column<double>  (type: "REAL",     nullable: false),
                    MentionCount   = table.Column<int>     (type: "INTEGER",  nullable: false),
                    Timestamp      = table.Column<DateTime>(type: "TEXT",     nullable: false),
                    RawSnippet     = table.Column<string>  (type: "TEXT",     nullable: false, maxLength: 2000)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentimentReadings", x => x.Id);
                    table.ForeignKey("FK_SentimentReadings_Coins_CoinId", x => x.CoinId, "Coins", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_SentimentReadings_CoinId", "SentimentReadings", "CoinId");

            // Signals
            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id                      = table.Column<int>     (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    CoinId                  = table.Column<int>     (type: "INTEGER", nullable: false),
                    NarrativeId             = table.Column<int>     (type: "INTEGER", nullable: true),
                    Timeframe               = table.Column<string>  (type: "TEXT",    nullable: false),
                    TaScore                 = table.Column<double>  (type: "REAL",    nullable: false),
                    SentimentScore          = table.Column<double>  (type: "REAL",    nullable: false),
                    MarketRegimeMultiplier  = table.Column<double>  (type: "REAL",    nullable: false),
                    CombinedScore           = table.Column<double>  (type: "REAL",    nullable: false),
                    Direction               = table.Column<string>  (type: "TEXT",    nullable: false),
                    Reasoning               = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 4000),
                    CreatedAt               = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    Acknowledged            = table.Column<bool>    (type: "INTEGER", nullable: false),
                    ActedOn                 = table.Column<bool>    (type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => x.Id);
                    table.ForeignKey("FK_Signals_Coins_CoinId", x => x.CoinId, "Coins", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_Signals_Narratives_NarrativeId", x => x.NarrativeId, "Narratives", "Id", onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_Signals_CoinId",      "Signals", "CoinId");
            migrationBuilder.CreateIndex("IX_Signals_NarrativeId",  "Signals", "NarrativeId");

            // SignalRules
            migrationBuilder.CreateTable(
                name: "SignalRules",
                columns: table => new
                {
                    Id                      = table.Column<int>   (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    NarrativeId             = table.Column<int>   (type: "INTEGER", nullable: true),
                    Name                    = table.Column<string>(type: "TEXT",    nullable: false, maxLength: 200),
                    IndicatorConditionsJson = table.Column<string>(type: "TEXT",    nullable: false, maxLength: 4000),
                    SentimentThreshold      = table.Column<double>(type: "REAL",    nullable: false),
                    ScoreThreshold          = table.Column<double>(type: "REAL",    nullable: false),
                    IsActive                = table.Column<bool>  (type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalRules", x => x.Id);
                    table.ForeignKey("FK_SignalRules_Narratives_NarrativeId", x => x.NarrativeId, "Narratives", "Id", onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_SignalRules_NarrativeId", "SignalRules", "NarrativeId");

            // ExchangeOrders
            migrationBuilder.CreateTable(
                name: "ExchangeOrders",
                columns: table => new
                {
                    Id              = table.Column<int>     (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    SignalId        = table.Column<int>     (type: "INTEGER", nullable: true),
                    Exchange        = table.Column<string>  (type: "TEXT",    nullable: false),
                    Symbol          = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 50),
                    Side            = table.Column<string>  (type: "TEXT",    nullable: false),
                    Type            = table.Column<string>  (type: "TEXT",    nullable: false),
                    Qty             = table.Column<double>  (type: "REAL",    nullable: false),
                    Entry           = table.Column<double>  (type: "REAL",    nullable: false),
                    StopLoss        = table.Column<double>  (type: "REAL",    nullable: false),
                    TakeProfit      = table.Column<double>  (type: "REAL",    nullable: false),
                    Status          = table.Column<string>  (type: "TEXT",    nullable: false),
                    ExternalOrderId = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 200),
                    IsPaper         = table.Column<bool>    (type: "INTEGER", nullable: false),
                    CreatedAt       = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    FilledAt        = table.Column<DateTime>(type: "TEXT",    nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeOrders", x => x.Id);
                    table.ForeignKey("FK_ExchangeOrders_Signals_SignalId", x => x.SignalId, "Signals", "Id", onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_ExchangeOrders_SignalId", "ExchangeOrders", "SignalId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("ExchangeOrders");
            migrationBuilder.DropTable("SignalRules");
            migrationBuilder.DropTable("Signals");
            migrationBuilder.DropTable("SentimentReadings");
            migrationBuilder.DropTable("ExchangeAccounts");
            migrationBuilder.DropTable("BronSources");

            migrationBuilder.DropColumn("Macd",                 "Coins");
            migrationBuilder.DropColumn("MacdSignal",           "Coins");
            migrationBuilder.DropColumn("BollingerUpper",       "Coins");
            migrationBuilder.DropColumn("BollingerLower",       "Coins");
            migrationBuilder.DropColumn("Atr",                  "Coins");
            migrationBuilder.DropColumn("StochRsi",             "Coins");
            migrationBuilder.DropColumn("LatestSentimentScore", "Coins");
            migrationBuilder.DropColumn("LatestSignalScore",    "Coins");
            migrationBuilder.DropColumn("MarketRegime",         "Coins");
        }
    }
}
