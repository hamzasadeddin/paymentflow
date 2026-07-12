using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

public enum PaymentStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Processing = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7,
    Rejected = 8
}

/// <summary>
/// A payment debits a source <see cref="PaymentAccount"/> and pays an Approved
/// <see cref="Beneficiary"/>. The lifecycle is enforced by the explicit
/// transition methods below — the Application layer never mutates
/// <see cref="Status"/> directly. Funds are reserved on Approve and settled on
/// Complete (see the Phase 03 design doc for the money semantics).
/// </summary>
public class Payment : AuditableEntity
{
    public string PaymentReference { get; set; } = string.Empty;

    public Guid SourceAccountId { get; set; }
    public PaymentAccount? SourceAccount { get; set; }

    public Guid BeneficiaryId { get; set; }
    public Beneficiary? Beneficiary { get; set; }

    // Money is always decimal, mapped decimal(19,4). Never float/double.
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    public string? Description { get; set; }

    /// <summary>Client-supplied (or server-defaulted) key that makes creation idempotent.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>The maker: the user who raised this payment. Used to block self-approval.</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// Number of distinct approvers required before this payment can reach
    /// <see cref="PaymentStatus.Approved"/>. Resolved from the approval policy
    /// and stamped at submit time, so a later threshold change never re-scopes an
    /// in-flight payment. 0 means the payment auto-approves on submit.
    /// </summary>
    public int RequiredApprovals { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Draft;

    public string? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }

    /// <summary>Set only when the payment reaches <see cref="PaymentStatus.Failed"/>.</summary>
    public string? FailureReason { get; private set; }

    public bool CanCancel => Status is PaymentStatus.Draft or PaymentStatus.PendingApproval;

    // ---------- Lifecycle transitions ----------

    /// <summary>
    /// Moves a draft payment to <see cref="PaymentStatus.PendingApproval"/> and
    /// locks in how many approvals it will require (resolved from the approval
    /// policy by the caller). The status flip to Approved still happens only via
    /// <see cref="Approve"/>, once the required number of approvals is reached.
    /// </summary>
    public void SubmitForApproval(int requiredApprovals, DateTime utcNow)
    {
        if (Status != PaymentStatus.Draft)
            throw new InvalidOperationException($"Cannot submit a payment in status {Status}.");
        if (requiredApprovals < 0)
            throw new InvalidOperationException("Required approvals cannot be negative.");

        RequiredApprovals = requiredApprovals;
        Status = PaymentStatus.PendingApproval;
        Touch(utcNow);
    }

    public void Approve(string reviewerUserId, string? notes, DateTime utcNow)
    {
        if (Status != PaymentStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve a payment in status {Status}.");

        Status = PaymentStatus.Approved;
        Stamp(reviewerUserId, notes, utcNow);
    }

    public void Reject(string reviewerUserId, string? notes, DateTime utcNow)
    {
        if (Status != PaymentStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject a payment in status {Status}.");

        Status = PaymentStatus.Rejected;
        Stamp(reviewerUserId, notes, utcNow);
    }

    public void Cancel(DateTime utcNow)
    {
        if (!CanCancel)
            throw new InvalidOperationException($"Cannot cancel a payment in status {Status}.");

        Status = PaymentStatus.Cancelled;
        Touch(utcNow);
    }

    // ---------- Processing transitions (wired to the API in Phase 05) ----------

    public void MarkProcessing(DateTime utcNow)
    {
        if (Status != PaymentStatus.Approved)
            throw new InvalidOperationException($"Cannot process a payment in status {Status}.");

        Status = PaymentStatus.Processing;
        Touch(utcNow);
    }

    public void Complete(DateTime utcNow)
    {
        if (Status != PaymentStatus.Processing)
            throw new InvalidOperationException($"Cannot complete a payment in status {Status}.");

        Status = PaymentStatus.Completed;
        Touch(utcNow);
    }

    public void Fail(string reason, DateTime utcNow)
    {
        if (Status != PaymentStatus.Processing)
            throw new InvalidOperationException($"Cannot fail a payment in status {Status}.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        Touch(utcNow);
    }

    private void Stamp(string reviewerUserId, string? notes, DateTime utcNow)
    {
        ReviewedByUserId = reviewerUserId;
        ReviewNotes = notes;
        ReviewedAtUtc = utcNow;
        Touch(utcNow);
    }
}
