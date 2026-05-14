using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoPortfolioTracker.Migrations
{
    public partial class AddExtendedIndicators : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extended indicator columns on Coins — previously [NotMapped], now persisted
            migrationBuilder.AddColumn<double>("Rsi",            "Coins", "REAL",    nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<string>("EmaCross",       "Coins", "TEXT",    nullable: false, defaultValue: "–");
            migrationBuilder.AddColumn<int>   ("EmaCrossBarsAgo","Coins", "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<double>("BollingerPctB",  "Coins", "REAL",    nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("Ma50DistPerc",   "Coins", "REAL",    nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("Adx",            "Coins", "REAL",    nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<bool>  ("IsSqueeze",      "Coins", "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<double>("High52wPerc",    "Coins", "REAL",    nullable: false, defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("Rsi",            "Coins");
            migrationBuilder.DropColumn("EmaCross",       "Coins");
            migrationBuilder.DropColumn("EmaCrossBarsAgo","Coins");
            migrationBuilder.DropColumn("BollingerPctB",  "Coins");
            migrationBuilder.DropColumn("Ma50DistPerc",   "Coins");
            migrationBuilder.DropColumn("Adx",            "Coins");
            migrationBuilder.DropColumn("IsSqueeze",      "Coins");
            migrationBuilder.DropColumn("High52wPerc",    "Coins");
        }
    }
}
