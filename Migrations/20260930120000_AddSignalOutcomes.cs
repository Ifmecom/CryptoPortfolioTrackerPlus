using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    // Documenteert het SignalOutcomes-schema (v1.46 — signal-outcome-tracker / score-kalibratie).
    // De daadwerkelijke aanmaak gebeurt idempotent via PortfolioService.ApplyPlusSchemaAsync
    // (CREATE TABLE IF NOT EXISTS), consistent met de overige PLUS-features. Bewust géén
    // [Migration]-attribuut, zodat MigrateAsync deze migratie negeert.
    public partial class AddSignalOutcomes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalOutcomes",
                columns: table => new
                {
                    Id              = table.Column<int>     (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Source          = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 20),
                    SourceRefId     = table.Column<int>     (type: "INTEGER", nullable: true),
                    CoinApiId       = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 200),
                    CoinSymbol      = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 50),
                    Direction       = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 10),
                    Score           = table.Column<double>  (type: "REAL",    nullable: false),
                    MarketRegime    = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 20),
                    SignalAt        = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    SignalDay       = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    EntryPrice      = table.Column<double>  (type: "REAL",    nullable: false),
                    Return1d        = table.Column<double>  (type: "REAL",    nullable: true),
                    Return3d        = table.Column<double>  (type: "REAL",    nullable: true),
                    Return7d        = table.Column<double>  (type: "REAL",    nullable: true),
                    Return14d       = table.Column<double>  (type: "REAL",    nullable: true),
                    MaxFavorablePct = table.Column<double>  (type: "REAL",    nullable: true),
                    MaxAdversePct   = table.Column<double>  (type: "REAL",    nullable: true),
                    IsComplete      = table.Column<bool>    (type: "INTEGER", nullable: false),
                    FailedAttempts  = table.Column<int>     (type: "INTEGER", nullable: false),
                    EvaluatedAt     = table.Column<DateTime>(type: "TEXT",    nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_SignalOutcomes", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_SignalOutcomes_Source_CoinApiId_SignalDay",
                table: "SignalOutcomes",
                columns: new[] { "Source", "CoinApiId", "SignalDay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalOutcomes_IsComplete",
                table: "SignalOutcomes",
                column: "IsComplete");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SignalOutcomes");
        }
    }
}
