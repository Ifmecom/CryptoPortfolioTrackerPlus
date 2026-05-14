using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class BronSourceEntityTypeConfiguration : IEntityTypeConfiguration<BronSource>
{
    public void Configure(EntityTypeBuilder<BronSource> configuration)
    {
        configuration.ToTable("BronSources");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Type).HasConversion<string>();
        configuration.Property(x => x.Url).HasMaxLength(500);
        configuration.Property(x => x.Handle).HasMaxLength(200);
    }
}
