using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Domain.Common;
using PaymentFlow.Domain.Entities;
using PaymentFlow.Infrastructure.Identity;

namespace PaymentFlow.Infrastructure.Persistence;

public class PaymentFlowDbContext(DbContextOptions<PaymentFlowDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PaymentFlowDbContext).Assembly);

        // On SQL Server, promote RowVersion to a true server-generated rowversion
        // column. Other providers (SQLite in tests) keep it as a plain concurrency
        // token that we stamp manually in SaveChanges.
        if (Database.IsSqlServer())
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var rowVersion = entityType.FindProperty(nameof(AuditableEntity.RowVersion));
                if (rowVersion is not null && rowVersion.ClrType == typeof(byte[]))
                {
                    rowVersion.SetColumnType("rowversion");
                    rowVersion.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
                }
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampRowVersionsForNonSqlServer();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// SQL Server auto-populates rowversion. Providers used in tests (SQLite,
    /// in-memory) don't, so we assign a fresh token on insert/update to keep the
    /// optimistic-concurrency path exercised. No-op on SQL Server.
    /// </summary>
    private void StampRowVersionsForNonSqlServer()
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
            return;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }
}
