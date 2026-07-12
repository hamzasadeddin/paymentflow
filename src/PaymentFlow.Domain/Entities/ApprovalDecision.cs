using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

public enum ApprovalSubjectType { Payment = 1, Beneficiary = 2 }

public enum ApprovalOutcome { Approved = 1, Rejected = 2 }

/// <summary>
/// Append-only record of a single approve/reject decision taken against a
/// payment or beneficiary. Styled after <see cref="SecurityAuditEvent"/> (no FK)
/// so it can span subject types and never blocks a delete.
///
/// The set of <b>distinct</b> <see cref="ApprovalOutcome.Approved"/> decisions
/// for a subject is the source of truth for dual-control progress, and
/// separation of duties (maker ≠ checker, and no checker twice) is enforced
/// against these records in the Application layer.
/// </summary>
public class ApprovalDecision : BaseEntity
{
    public ApprovalSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>Identity of the approver (a user id string, or <see cref="AutoApprover"/>).</summary>
    public string ApproverUserId { get; set; } = string.Empty;
    public string? ApproverEmail { get; set; }

    public ApprovalOutcome Decision { get; set; }
    public string? Notes { get; set; }

    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Marker approver used for the auto-approve (below-threshold) path.</summary>
    public const string AutoApprover = "policy:auto";
}
