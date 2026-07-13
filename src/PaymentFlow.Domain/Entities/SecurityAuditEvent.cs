using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

public class SecurityAuditEvent : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public static class SecurityEventTypes
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string AccountLockedOut = "AccountLockedOut";
    public const string TokenRefreshed = "TokenRefreshed";
    public const string TokenReplayDetected = "TokenReplayDetected";
    public const string Logout = "Logout";

    public const string PaymentApproved = "PaymentApproved";
    public const string PaymentRejected = "PaymentRejected";
    public const string PaymentCompleted = "PaymentCompleted";
    public const string PaymentFailed = "PaymentFailed";
    public const string BeneficiaryApproved = "BeneficiaryApproved";
    public const string BeneficiaryRejected = "BeneficiaryRejected";

    // Phase 06 — compliance & reconciliation.
    public const string ComplianceHoldPlaced = "ComplianceHoldPlaced";
    public const string ComplianceHoldCleared = "ComplianceHoldCleared";
    public const string ComplianceHoldRejected = "ComplianceHoldRejected";
    public const string ReconciliationRunCompleted = "ReconciliationRunCompleted";
    public const string ReconciliationBreakResolved = "ReconciliationBreakResolved";
    public const string ReconciliationBreakIgnored = "ReconciliationBreakIgnored";
}
