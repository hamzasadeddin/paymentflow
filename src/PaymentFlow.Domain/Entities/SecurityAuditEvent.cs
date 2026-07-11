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
}
