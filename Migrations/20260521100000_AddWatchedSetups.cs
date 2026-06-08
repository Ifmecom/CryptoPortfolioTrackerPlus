using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    public partial class AddWatchedSetups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchedSetups",
                columns: table => new
                {
                    Id             = table.Column<int>    (type: "INTEGER", nullable: false)
                                         .Annotation("Sqlite:Autoincrement", true),
                    CoinApiId      = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 200),
                    CoinName       = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 200),
                    CoinSymbol     = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 50),
                    ImageUri       = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 500),
                    Direction      = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 10),
                    EntryPrice     = table.Column<double> (type: "REAL",    nullable: false),
                    StopLoss       = table.Column<double> (type: "REAL",    nullable: false),
                    Target1        = table.Column<double> (type: "REAL",    nullable: false),
                    Target2        = table.Column<double> (type: "REAL",    nullable: false),
                    Score          = table.Column<int>    (type: "INTEGER", nullable: false),
                    PatternSummary = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 500),
                    Bias1D         = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 20),
                    Bias4H         = table.Column<string> (type: "TEXT",    nullable: false, maxLength: 20),
                    AddedAt        = table.Column<DateTime>(type: "TEXT",   nullable: false),
                    Status         = table.Column<int>    (type: "INTEGER", nullable: false, defaultValue: 0),
                    ClosePrice     = table.Column<double> (type: "REAL",    nullable: true),
                    ClosedAt       = table.Column<DateTime>(type: "TEXT",   nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_WatchedSetups", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_WatchedSetups_Status",
                table: "WatchedSetups",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedSetups_CoinApiId",
                table: "WatchedSetups",
                column: "CoinApiId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WatchedSetups");
        }
    }
}
