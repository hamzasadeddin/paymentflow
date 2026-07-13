using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

/// <summary>
/// A single reconciliation pass: settled (<see cref="PaymentStatus.Completed"/>)
/// payments matched against a simulated external bank statement. The run records
/// summary counts; each difference is a <see cref="ReconciliationBreak"/> child.
/// </summary>
public class ReconciliationRun : AuditableEntity
{
    public string RunReference { get; set; } = string.Empty;   // RECON-{year}-{seq:D6}
    public DateTime StatementDateUtc { get; set; }
    public string? RunByUserId { get; set; }

    public int MatchedCount { get; set; }
    public int BreakCount { get; set; }

    public DateTime CompletedAtUtc { get; set; }
}

/// <summary>The kind of mismatch found between the ledger and the statement.</summary>
public enum BreakType
{
    /// <summary>A completed payment with no matching statement line.</summary>
    MissingFromStatement = 1,

    /// <summary>A statement line with no matching completed payment.</summary>
    MissingFromLedger = 2,

    /// <summary>Both sides present but the amounts differ.</summary>
    AmountMismatch = 3
}

/// <summary>Lifecycle of a break: open until an operator resolves or ignores it.</summary>
public enum BreakStatus { Open = 1, Resolved = 2, Ignored = 3 }

/// <summary>
/// A single reconciliation difference. Carries whichever side(s) exist and their
/// amounts, so the UI can show the discrepancy without another lookup. Resolve /
/// ignore are explicit transitions that throw on an invalid change (→ 409), and
/// the <see cref="AuditableEntity.RowVersion"/> guards concurrent resolution.
/// </summary>
public class ReconciliationBreak : AuditableEntity
{
    public Guid RunId { get; set; }
    public BreakType Type { get; set; }

    public Guid? PaymentId { get; set; }
    public string? PaymentReference { get; set; }
    public string? StatementReference { get; set; }

    public decimal? LedgerAmount { get; set; }
    public decimal? StatementAmount { get; set; }
    public string Currency { get; set; } = "USD";

    public BreakStatus Status { get; private set; } = BreakStatus.Open;

    public string? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public void Resolve(string userId, string? notes, DateTime utcNow)
    {
        if (Status != BreakStatus.Open)
            throw new InvalidOperationException($"Cannot resolve a break in status {Status}.");

        Status = BreakStatus.Resolved;
        Stamp(userId, notes, utcNow);
    }

    public void Ignore(string userId, string? notes, DateTime utcNow)
    {
        if (Status != BreakStatus.Open)
            throw new InvalidOperationException($"Cannot ignore a break in status {Status}.");

        Status = BreakStatus.Ignored;
        Stamp(userId, notes, utcNow);
    }

    private void Stamp(string userId, string? notes, DateTime utcNow)
    {
        ResolvedByUserId = userId;
        ResolutionNotes = notes;
        ResolvedAtUtc = utcNow;
        Touch(utcNow);
    }
}
