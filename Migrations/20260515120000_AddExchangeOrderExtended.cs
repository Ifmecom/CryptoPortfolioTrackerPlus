using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeOrderExtended : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TakeProfit2",
                table: "ExchangeOrders",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Leverage",
                table: "ExchangeOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MarketType",
                table: "ExchangeOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);   // 0 = Spot
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TakeProfit2", table: "ExchangeOrders");
            migrationBuilder.DropColumn(name: "Leverage",    table: "ExchangeOrders");
            migrationBuilder.DropColumn(name: "MarketType",  table: "ExchangeOrders");
        }
    }
}
