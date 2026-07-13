using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

/// <summary>Why a payment was flagged for compliance review.</summary>
public enum ComplianceCategory { Sanctions = 1, Limit = 2, Manual = 3 }

/// <summary>Lifecycle of a compliance hold: open until an officer decides.</summary>
public enum ComplianceCaseStatus { Open = 1, Cleared = 2, Rejected = 3 }

/// <summary>
/// A compliance hold raised against a payment (by automatic screening at submit,
/// or manually). It is a <b>gate</b>, not a payment state: while a case is
/// <see cref="ComplianceCaseStatus.Open"/> or <see cref="ComplianceCaseStatus.Rejected"/>
/// the payment cannot settle (enforced in the Application layer). Clearing the
/// case unblocks settlement; rejecting it blocks the payment permanently (operations
/// then cancels it through the normal path).
///
/// Tied to a payment by id + a denormalized reference snapshot for display — no FK,
/// the same styling as <see cref="ApprovalDecision"/> / <see cref="SecurityAuditEvent"/>,
/// so it never blocks a delete and stays audit-friendly. Transitions are explicit
/// methods that throw on an invalid change; the Application layer maps that to a 409.
/// </summary>
public class ComplianceCase : AuditableEntity
{
    public Guid PaymentId { get; set; }

    /// <summary>Snapshot of the payment reference at raise time, for display without a join.</summary>
    public string PaymentReference { get; set; } = string.Empty;

    public ComplianceCategory Category { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>The raiser: a user id string, or <c>null</c> when raised by automatic screening.</summary>
    public string? RaisedByUserId { get; set; }

    public ComplianceCaseStatus Status { get; private set; } = ComplianceCaseStatus.Open;

    public string? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }

    /// <summary>True while this case blocks settlement of its payment.</summary>
    public bool IsBlocking => Status is ComplianceCaseStatus.Open or ComplianceCaseStatus.Rejected;

    public void Clear(string reviewerUserId, string? notes, DateTime utcNow)
    {
        if (Status != ComplianceCaseStatus.Open)
            throw new InvalidOperationException($"Cannot clear a compliance case in status {Status}.");

        Status = ComplianceCaseStatus.Cleared;
        Stamp(reviewerUserId, notes, utcNow);
    }

    public void Reject(string reviewerUserId, string? notes, DateTime utcNow)
    {
        if (Status != ComplianceCaseStatus.Open)
            throw new InvalidOperationException($"Cannot reject a compliance case in status {Status}.");

        Status = ComplianceCaseStatus.Rejected;
        Stamp(reviewerUserId, notes, utcNow);
    }

    private void Stamp(string reviewerUserId, string? notes, DateTime utcNow)
    {
        ReviewedByUserId = reviewerUserId;
        ReviewNotes = notes;
        ReviewedAtUtc = utcNow;
        Touch(utcNow);
    }
}
