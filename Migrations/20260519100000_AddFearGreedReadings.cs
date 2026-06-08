using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddFearGreedReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FearGreedReadings",
                columns: table => new
                {
                    Id             = table.Column<int>(type: "INTEGER", nullable: false)
                                         .Annotation("Sqlite:Autoincrement", true),
                    Value          = table.Column<int>(type: "INTEGER", nullable: false),
                    Classification = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp      = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FearGreedReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FearGreedReadings_Timestamp",
                table: "FearGreedReadings",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FearGreedReadings");
        }
    }
}
