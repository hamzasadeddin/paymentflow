namespace PaymentFlow.Application.Features.Audit;

/// <summary>A single security-audit row for the read-only audit-log viewer.</summary>
public record AuditEventDto(
    Guid Id,
    Guid? UserId,
    string? Email,
    string EventType,
    bool Succeeded,
    string? IpAddress,
    string? Details,
    DateTime OccurredAtUtc);

/// <summary>One group of related event types, for populating the viewer's filter dropdown.</summary>
public record AuditEventTypeGroupDto(string Group, IReadOnlyList<string> Types);
