using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SentimentReadings: filter op CoinId en tijdreeks-queries op Timestamp
            migrationBuilder.CreateIndex(
                name: "IX_SentimentReadings_CoinId",
                table: "SentimentReadings",
                column: "CoinId");

            migrationBuilder.CreateIndex(
                name: "IX_SentimentReadings_Timestamp",
                table: "SentimentReadings",
                column: "Timestamp");

            // Signals: dashboard-queries filteren op CoinId + recente CreatedAt
            migrationBuilder.CreateIndex(
                name: "IX_Signals_CoinId",
                table: "Signals",
                column: "CoinId");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_CreatedAt",
                table: "Signals",
                column: "CreatedAt");

            // ExchangeOrders: compound index voor auto-close en journal-filter
            migrationBuilder.CreateIndex(
                name: "IX_ExchangeOrders_IsPaper_Status",
                table: "ExchangeOrders",
                columns: new[] { "IsPaper", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentimentReadings_CoinId",
                table: "SentimentReadings");

            migrationBuilder.DropIndex(
                name: "IX_SentimentReadings_Timestamp",
                table: "SentimentReadings");

            migrationBuilder.DropIndex(
                name: "IX_Signals_CoinId",
                table: "Signals");

            migrationBuilder.DropIndex(
                name: "IX_Signals_CreatedAt",
                table: "Signals");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeOrders_IsPaper_Status",
                table: "ExchangeOrders");
        }
    }
}
