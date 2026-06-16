using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class PatternStateRecordEntityTypeConfiguration : IEntityTypeConfiguration<PatternStateRecord>
{
    public void Configure(EntityTypeBuilder<PatternStateRecord> configuration)
    {
        configuration.ToTable("PatternStates");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();

        configuration.Property(x => x.Fingerprint).HasMaxLength(200);
        configuration.Property(x => x.CoinApiId).HasMaxLength(200);
        configuration.Property(x => x.CoinSymbol).HasMaxLength(50);
        configuration.Property(x => x.Timeframe).HasMaxLength(10);
        configuration.Property(x => x.LastDescription).HasMaxLength(1000);
        configuration.Property(x => x.LastTransitionReason).HasMaxLength(500);

        // Enums als int opslaan (consistent met WatchedSetup.Status).
        configuration.Property(x => x.Type).HasConversion<int>();
        configuration.Property(x => x.Category).HasConversion<int>();
        configuration.Property(x => x.Lifecycle).HasConversion<int>();
        configuration.Property(x => x.NotifiedLifecycle).HasConversion<int>();

        configuration.Property(x => x.LastTransitionAt).IsRequired(false);

        // Snelle lookup: actieve records per coin en match op fingerprint tijdens reconciliatie.
        configuration.HasIndex(x => x.Fingerprint);
        configuration.HasIndex(x => x.CoinApiId);
        configuration.HasIndex(x => x.IsActive);
    }
}
