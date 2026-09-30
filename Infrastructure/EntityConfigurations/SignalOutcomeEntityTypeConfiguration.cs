using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class SignalOutcomeEntityTypeConfiguration : IEntityTypeConfiguration<SignalOutcome>
{
    public void Configure(EntityTypeBuilder<SignalOutcome> configuration)
    {
        configuration.ToTable("SignalOutcomes");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();

        configuration.Property(x => x.Source).HasMaxLength(20);
        configuration.Property(x => x.CoinApiId).HasMaxLength(200);
        configuration.Property(x => x.CoinSymbol).HasMaxLength(50);
        configuration.Property(x => x.Direction).HasMaxLength(10);
        configuration.Property(x => x.MarketRegime).HasMaxLength(20);

        configuration.Property(x => x.SourceRefId).IsRequired(false);
        configuration.Property(x => x.EvaluatedAt).IsRequired(false);

        // Eén meting per bron + coin + dag (ontdubbeling), en snel de openstaande rijen vinden.
        configuration.HasIndex(x => new { x.Source, x.CoinApiId, x.SignalDay }).IsUnique();
        configuration.HasIndex(x => x.IsComplete);
    }
}
