using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class FearGreedReadingEntityTypeConfiguration : IEntityTypeConfiguration<FearGreedReading>
{
    public void Configure(EntityTypeBuilder<FearGreedReading> configuration)
    {
        configuration.ToTable("FearGreedReadings");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Classification).HasMaxLength(50);
        configuration.Property(x => x.Timestamp).HasColumnType("TEXT");

        // Dashboard query: meest recente lezing ophalen
        configuration.HasIndex(x => x.Timestamp);
    }
}
