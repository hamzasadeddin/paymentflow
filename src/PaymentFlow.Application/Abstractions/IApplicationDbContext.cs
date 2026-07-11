using Microsoft.EntityFrameworkCore;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Abstractions;

/// <summary>
/// Application-facing view of the database. Exposing DbSets directly avoids a
/// redundant repository layer: EF Core already provides unit-of-work + querying.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<PaymentAccount> PaymentAccounts { get; }
    DbSet<Beneficiary> Beneficiaries { get; }
    DbSet<Payment> Payments { get; }
    DbSet<SecurityAuditEvent> SecurityAuditEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
