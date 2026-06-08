using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class CoinFundamentalsEntityTypeConfiguration : IEntityTypeConfiguration<CoinFundamentals>
{
    public void Configure(EntityTypeBuilder<CoinFundamentals> configuration)
    {
        configuration.ToTable("CoinFundamentals");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();

        configuration.Property(x => x.ApiId).HasMaxLength(120).IsRequired();
        configuration.HasIndex(x => x.ApiId).IsUnique();   // één rij per coin

        configuration.Property(x => x.Symbol).HasMaxLength(40);
        configuration.Property(x => x.Name).HasMaxLength(120);
        configuration.Property(x => x.Categories).HasMaxLength(1000);
        configuration.Property(x => x.Verdict).HasMaxLength(60);

        // Datums als TEXT (consistent met overige entiteiten / SQLite)
        configuration.Property(x => x.GenesisDate).HasColumnType("TEXT");
        configuration.Property(x => x.AthDate).HasColumnType("TEXT");
        configuration.Property(x => x.AtlDate).HasColumnType("TEXT");
        configuration.Property(x => x.UpdatedAt).HasColumnType("TEXT");
    }
}
