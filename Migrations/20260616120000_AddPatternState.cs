using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    // Documenteert het PatternStates-schema (P7 — continue invalidatie met patroon-geheugen).
    // De daadwerkelijke aanmaak gebeurt idempotent via PortfolioService.ApplyPlusSchemaAsync
    // (CREATE TABLE IF NOT EXISTS), consistent met de overige PLUS-features.
    public partial class AddPatternState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatternStates",
                columns: table => new
                {
                    Id                   = table.Column<int>     (type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Fingerprint          = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 200),
                    CoinApiId            = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 200),
                    CoinSymbol           = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 50),
                    Timeframe            = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 10),
                    Type                 = table.Column<int>     (type: "INTEGER", nullable: false),
                    Category             = table.Column<int>     (type: "INTEGER", nullable: false),
                    KeyLevel             = table.Column<double>  (type: "REAL",    nullable: false),
                    Strength             = table.Column<int>     (type: "INTEGER", nullable: false),
                    LastDescription      = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 1000),
                    Lifecycle            = table.Column<int>     (type: "INTEGER", nullable: false),
                    IsActive             = table.Column<bool>    (type: "INTEGER", nullable: false),
                    FirstSeenAt          = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    LastSeenAt           = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    LastScanAt           = table.Column<DateTime>(type: "TEXT",    nullable: false),
                    TimesSeen            = table.Column<int>     (type: "INTEGER", nullable: false),
                    MissedScans          = table.Column<int>     (type: "INTEGER", nullable: false),
                    LastTransitionReason = table.Column<string>  (type: "TEXT",    nullable: false, maxLength: 500),
                    LastTransitionAt     = table.Column<DateTime>(type: "TEXT",    nullable: true),
                    NotifiedLifecycle    = table.Column<int>     (type: "INTEGER", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_PatternStates", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PatternStates_Fingerprint",
                table: "PatternStates",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_PatternStates_CoinApiId",
                table: "PatternStates",
                column: "CoinApiId");

            migrationBuilder.CreateIndex(
                name: "IX_PatternStates_IsActive",
                table: "PatternStates",
                column: "IsActive");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PatternStates");
        }
    }
}
