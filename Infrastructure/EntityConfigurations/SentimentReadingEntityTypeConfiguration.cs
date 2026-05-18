using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class SentimentReadingEntityTypeConfiguration : IEntityTypeConfiguration<SentimentReading>
{
    public void Configure(EntityTypeBuilder<SentimentReading> configuration)
    {
        configuration.ToTable("SentimentReadings");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Source).HasConversion<string>();
        configuration.Property(x => x.RawSnippet).HasMaxLength(2000);

        configuration.HasOne(x => x.Coin)
            .WithMany()
            .HasForeignKey(x => x.CoinId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexen voor veel-gebruikte filtercombinaties
        configuration.HasIndex(x => x.CoinId);
        configuration.HasIndex(x => x.Timestamp);
    }
}
