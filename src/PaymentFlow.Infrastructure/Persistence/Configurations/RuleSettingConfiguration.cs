using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence.Configurations;

public sealed class RuleSettingConfiguration : IEntityTypeConfiguration<RuleSetting>
{
    public void Configure(EntityTypeBuilder<RuleSetting> builder)
    {
        builder.ToTable("RuleSettings");

        builder.Property(r => r.Section).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ValueJson).IsRequired();
        builder.Property(r => r.UpdatedByUserId).HasMaxLength(64);

        builder.Property(r => r.RowVersion).IsConcurrencyToken();

        // One override row per section.
        builder.HasIndex(r => r.Section).IsUnique();
    }
}
