using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class SignalEntityTypeConfiguration : IEntityTypeConfiguration<Signal>
{
    public void Configure(EntityTypeBuilder<Signal> configuration)
    {
        configuration.ToTable("Signals");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Direction).HasConversion<string>();
        configuration.Property(x => x.Timeframe).HasConversion<string>();
        configuration.Property(x => x.Reasoning).HasMaxLength(4000);

        configuration.HasOne(x => x.Coin)
            .WithMany()
            .HasForeignKey(x => x.CoinId)
            .OnDelete(DeleteBehavior.Cascade);

        configuration.HasOne(x => x.Narrative)
            .WithMany()
            .HasForeignKey(x => x.NarrativeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
