using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence.Configurations;

public sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecisions");

        builder.Property(d => d.SubjectType).HasConversion<int>();
        builder.Property(d => d.Decision).HasConversion<int>();
        builder.Property(d => d.ApproverUserId).HasMaxLength(64).IsRequired();
        builder.Property(d => d.ApproverEmail).HasMaxLength(256);
        builder.Property(d => d.Notes).HasMaxLength(1024);

        // Fast per-subject lookups (dual-control progress, decision history).
        builder.HasIndex(d => new { d.SubjectType, d.SubjectId });
    }
}
