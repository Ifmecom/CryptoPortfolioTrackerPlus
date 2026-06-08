using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class WatchedSetupEntityTypeConfiguration : IEntityTypeConfiguration<WatchedSetup>
{
    public void Configure(EntityTypeBuilder<WatchedSetup> configuration)
    {
        configuration.ToTable("WatchedSetups");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.CoinApiId).HasMaxLength(200);
        configuration.Property(x => x.CoinName).HasMaxLength(200);
        configuration.Property(x => x.CoinSymbol).HasMaxLength(50);
        configuration.Property(x => x.ImageUri).HasMaxLength(500);
        configuration.Property(x => x.Direction).HasMaxLength(10);
        configuration.Property(x => x.PatternSummary).HasMaxLength(500);
        configuration.Property(x => x.Bias1D).HasMaxLength(20);
        configuration.Property(x => x.Bias4H).HasMaxLength(20);
        configuration.Property(x => x.Status).HasConversion<int>();
        configuration.Property(x => x.MarketRegimeAtCreation).HasMaxLength(20);
        configuration.Property(x => x.EntryAt).IsRequired(false);

        // Index for fast lookup of active setups during auto-price check
        configuration.HasIndex(x => x.Status);
        configuration.HasIndex(x => x.CoinApiId);
    }
}
