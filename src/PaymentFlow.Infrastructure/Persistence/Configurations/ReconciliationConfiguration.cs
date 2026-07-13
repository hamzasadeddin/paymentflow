using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence.Configurations;

public sealed class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> builder)
    {
        builder.ToTable("ReconciliationRuns");

        builder.Property(r => r.RunReference).HasMaxLength(32).IsRequired();
        builder.Property(r => r.RunByUserId).HasMaxLength(64);
        builder.Property(r => r.RowVersion).IsConcurrencyToken();

        builder.HasIndex(r => r.RunReference).IsUnique();
        builder.HasIndex(r => r.CreatedAtUtc);
    }
}

public sealed class ReconciliationBreakConfiguration : IEntityTypeConfiguration<ReconciliationBreak>
{
    public void Configure(EntityTypeBuilder<ReconciliationBreak> builder)
    {
        builder.ToTable("ReconciliationBreaks");

        builder.Property(b => b.Type).HasConversion<int>();
        builder.Property(b => b.Status).HasConversion<int>();
        builder.Property(b => b.PaymentReference).HasMaxLength(32);
        builder.Property(b => b.StatementReference).HasMaxLength(32);
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();
        builder.Property(b => b.ResolvedByUserId).HasMaxLength(64);
        builder.Property(b => b.ResolutionNotes).HasMaxLength(1024);

        // Money as decimal(19,4) — never floating point.
        builder.Property(b => b.LedgerAmount).HasColumnType("decimal(19,4)");
        builder.Property(b => b.StatementAmount).HasColumnType("decimal(19,4)");
        builder.Property(b => b.RowVersion).IsConcurrencyToken();

        // Breaks are listed per run and filtered by status.
        builder.HasIndex(b => new { b.RunId, b.Status });

        builder.HasOne<ReconciliationRun>()
            .WithMany()
            .HasForeignKey(b => b.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
