using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    public partial class AddExchangeAccountRsa : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthMethod",
                table: "ExchangeAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "HMAC");

            migrationBuilder.AddColumn<string>(
                name: "PublicKeyPem",
                table: "ExchangeAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AuthMethod",   table: "ExchangeAccounts");
            migrationBuilder.DropColumn(name: "PublicKeyPem", table: "ExchangeAccounts");
        }
    }
}
