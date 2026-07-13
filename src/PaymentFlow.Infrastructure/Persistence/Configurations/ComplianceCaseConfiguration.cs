using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence.Configurations;

public sealed class ComplianceCaseConfiguration : IEntityTypeConfiguration<ComplianceCase>
{
    public void Configure(EntityTypeBuilder<ComplianceCase> builder)
    {
        builder.ToTable("ComplianceCases");

        builder.Property(c => c.PaymentReference).HasMaxLength(32).IsRequired();
        builder.Property(c => c.Category).HasConversion<int>();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.Reason).HasMaxLength(512).IsRequired();
        builder.Property(c => c.RaisedByUserId).HasMaxLength(64);
        builder.Property(c => c.ReviewedByUserId).HasMaxLength(64);
        builder.Property(c => c.ReviewNotes).HasMaxLength(1024);

        builder.Property(c => c.RowVersion).IsConcurrencyToken();

        builder.Ignore(c => c.IsBlocking);

        // Fast lookups: per-payment gate check and the review queue by status.
        builder.HasIndex(c => c.PaymentId);
        builder.HasIndex(c => c.Status);
    }
}
