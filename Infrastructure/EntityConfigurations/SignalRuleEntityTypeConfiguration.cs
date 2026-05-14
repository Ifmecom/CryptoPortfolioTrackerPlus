using CryptoPortfolioTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoPortfolioTracker.Infrastructure.EntityConfigurations;

class SignalRuleEntityTypeConfiguration : IEntityTypeConfiguration<SignalRule>
{
    public void Configure(EntityTypeBuilder<SignalRule> configuration)
    {
        configuration.ToTable("SignalRules");
        configuration.HasKey(x => x.Id);
        configuration.Property(x => x.Id).ValueGeneratedOnAdd();
        configuration.Property(x => x.Name).HasMaxLength(200);
        configuration.Property(x => x.IndicatorConditionsJson).HasMaxLength(4000);

        configuration.HasOne(x => x.Narrative)
            .WithMany()
            .HasForeignKey(x => x.NarrativeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
