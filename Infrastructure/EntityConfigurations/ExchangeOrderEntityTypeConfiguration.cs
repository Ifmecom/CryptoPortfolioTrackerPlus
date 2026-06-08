using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class ExchangeOrderEntityTypeConfiguration : IEntityTypeConfiguration<ExchangeOrder>
{
    public void Configure(EntityTypeBuilder<ExchangeOrder> configuration)
    {
        configuration.ToTable("ExchangeOrders");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Exchange).HasConversion<string>();
        configuration.Property(x => x.Side).HasConversion<string>();
        configuration.Property(x => x.Type).HasConversion<string>();
        configuration.Property(x => x.Status).HasConversion<string>();
        configuration.Property(x => x.Symbol).HasMaxLength(50);
        configuration.Property(x => x.ExternalOrderId).HasMaxLength(200);

        configuration.HasOne(x => x.Signal)
            .WithMany()
            .HasForeignKey(x => x.SignalId)
            .OnDelete(DeleteBehavior.SetNull);

        // Optional FK to WatchedSetup (no navigation property — avoids circular dependency)
        configuration.Property(x => x.WatchedSetupId).IsRequired(false);

        // Compound index voor auto-close en journal-filter (IsPaper + Status)
        configuration.HasIndex(x => new { x.IsPaper, x.Status });
    }
}
