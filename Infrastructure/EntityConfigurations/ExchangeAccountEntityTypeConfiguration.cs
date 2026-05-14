using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class ExchangeAccountEntityTypeConfiguration : IEntityTypeConfiguration<ExchangeAccount>
{
    public void Configure(EntityTypeBuilder<ExchangeAccount> configuration)
    {
        configuration.ToTable("ExchangeAccounts");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Exchange).HasConversion<string>();
        configuration.Property(x => x.Permissions).HasMaxLength(500);
    }
}
