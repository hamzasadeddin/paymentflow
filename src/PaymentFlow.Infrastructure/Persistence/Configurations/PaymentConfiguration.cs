using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.Property(p => p.PaymentReference).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(280);
        builder.Property(p => p.IdempotencyKey).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Status).HasConversion<int>();
        builder.Property(p => p.ReviewedByUserId).HasMaxLength(64);
        builder.Property(p => p.ReviewNotes).HasMaxLength(1024);
        builder.Property(p => p.FailureReason).HasMaxLength(1024);

        // Money as decimal(19,4) — never floating point.
        builder.Property(p => p.Amount).HasColumnType("decimal(19,4)");
        builder.Property(p => p.RowVersion).IsConcurrencyToken();

        builder.Ignore(p => p.CanCancel);

        builder.HasIndex(p => p.PaymentReference).IsUnique();
        builder.HasIndex(p => p.IdempotencyKey).IsUnique();
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.SourceAccountId);
        builder.HasIndex(p => p.BeneficiaryId);

        // Committed payments pin their account/beneficiary — no cascade delete.
        builder.HasOne(p => p.SourceAccount)
            .WithMany()
            .HasForeignKey(p => p.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Beneficiary)
            .WithMany()
            .HasForeignKey(p => p.BeneficiaryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
