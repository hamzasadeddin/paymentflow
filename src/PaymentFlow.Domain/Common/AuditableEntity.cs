namespace PaymentFlow.Domain.Common;

/// <summary>
/// Base for mutable records that need optimistic concurrency and change stamps.
/// RowVersion is mapped to a SQL Server rowversion; EF throws
/// DbUpdateConcurrencyException when a stale copy is saved.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public void Touch(DateTime utcNow) => UpdatedAtUtc = utcNow;
}
