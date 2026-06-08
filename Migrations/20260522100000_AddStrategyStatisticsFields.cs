using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    /// <summary>
    /// Adds strategy-statistics fields to WatchedSetups and ExchangeOrders
    /// to enable per-setup performance tracking and Setup ↔ Order linking.
    /// </summary>
    public partial class AddStrategyStatisticsFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── WatchedSetups ────────────────────────────────────────────────────

            // BTC market regime snapshot at setup creation time ("RiskOn" | "Neutral" | "RiskOff")
            migrationBuilder.AddColumn<string>(
                name:          "MarketRegimeAtCreation",
                table:         "WatchedSetups",
                type:          "TEXT",
                maxLength:     20,
                nullable:      true);

            // Whether TP2 was reached (auto-detected during price check cycles)
            migrationBuilder.AddColumn<bool>(
                name:          "Tp2Hit",
                table:         "WatchedSetups",
                type:          "INTEGER",
                nullable:      false,
                defaultValue:  false);

            // Optional FK to the ExchangeOrder that was opened from this setup
            migrationBuilder.AddColumn<int>(
                name:          "LinkedOrderId",
                table:         "WatchedSetups",
                type:          "INTEGER",
                nullable:      true);

            // ── ExchangeOrders ───────────────────────────────────────────────────

            // Optional back-reference to the WatchedSetup that spawned this order
            migrationBuilder.AddColumn<int>(
                name:          "WatchedSetupId",
                table:         "ExchangeOrders",
                type:          "INTEGER",
                nullable:      true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MarketRegimeAtCreation", table: "WatchedSetups");
            migrationBuilder.DropColumn(name: "Tp2Hit",                 table: "WatchedSetups");
            migrationBuilder.DropColumn(name: "LinkedOrderId",          table: "WatchedSetups");
            migrationBuilder.DropColumn(name: "WatchedSetupId",         table: "ExchangeOrders");
        }
    }
}
