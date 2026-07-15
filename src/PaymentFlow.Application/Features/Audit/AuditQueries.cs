using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Audit;

// ---------- Paged, filtered audit read ----------

/// <summary>
/// The read-only audit trail over <see cref="SecurityAuditEvent"/>, newest first,
/// with optional filters. Every filter is additive (AND). Purely a read — the
/// trail is append-only and never mutated here.
/// </summary>
public record GetAuditEventsQuery(
    int Page = 1,
    int PageSize = 25,
    string? EventType = null,
    bool? Succeeded = null,
    string? UserQuery = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null)
    : IRequest<Result<PagedResult<AuditEventDto>>>;

public sealed class GetAuditEventsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAuditEventsQuery, Result<PagedResult<AuditEventDto>>>
{
    public async Task<Result<PagedResult<AuditEventDto>>> Handle(
        GetAuditEventsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var query = db.SecurityAuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EventType))
            query = query.Where(e => e.EventType == request.EventType);

        if (request.Succeeded is not null)
            query = query.Where(e => e.Succeeded == request.Succeeded);

        if (!string.IsNullOrWhiteSpace(request.UserQuery))
        {
            var term = request.UserQuery.Trim();
            query = query.Where(e => e.Email != null && e.Email.Contains(term));
        }

        if (request.FromUtc is not null)
            query = query.Where(e => e.OccurredAtUtc >= request.FromUtc);
        if (request.ToUtc is not null)
            query = query.Where(e => e.OccurredAtUtc <= request.ToUtc);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(e =>
                (e.Details != null && e.Details.Contains(term)) ||
                (e.Email != null && e.Email.Contains(term)) ||
                e.EventType.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditEventDto(
                e.Id, e.UserId, e.Email, e.EventType, e.Succeeded,
                e.IpAddress, e.Details, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<AuditEventDto>(items, page, pageSize, totalCount));
    }
}

// ---------- Event-type catalog (filter dropdown) ----------

/// <summary>
/// The known audit event types grouped by area, so the viewer's filter dropdown
/// stays in step with the backend vocabulary (<see cref="SecurityEventTypes"/>).
/// </summary>
public record GetAuditEventTypesQuery : IRequest<Result<IReadOnlyList<AuditEventTypeGroupDto>>>;

public sealed class GetAuditEventTypesQueryHandler
    : IRequestHandler<GetAuditEventTypesQuery, Result<IReadOnlyList<AuditEventTypeGroupDto>>>
{
    private static readonly IReadOnlyList<AuditEventTypeGroupDto> Catalog =
    [
        new("Authentication",
        [
            SecurityEventTypes.LoginSucceeded, SecurityEventTypes.LoginFailed,
            SecurityEventTypes.AccountLockedOut, SecurityEventTypes.TokenRefreshed,
            SecurityEventTypes.TokenReplayDetected, SecurityEventTypes.Logout
        ]),
        new("Payments & approvals",
        [
            SecurityEventTypes.PaymentApproved, SecurityEventTypes.PaymentRejected,
            SecurityEventTypes.PaymentCompleted, SecurityEventTypes.PaymentFailed,
            SecurityEventTypes.BeneficiaryApproved, SecurityEventTypes.BeneficiaryRejected
        ]),
        new("Compliance & reconciliation",
        [
            SecurityEventTypes.ComplianceHoldPlaced, SecurityEventTypes.ComplianceHoldCleared,
            SecurityEventTypes.ComplianceHoldRejected, SecurityEventTypes.ReconciliationRunCompleted,
            SecurityEventTypes.ReconciliationBreakResolved, SecurityEventTypes.ReconciliationBreakIgnored
        ]),
        new("Administration",
        [
            SecurityEventTypes.UserCreated, SecurityEventTypes.UserActivated,
            SecurityEventTypes.UserDeactivated, SecurityEventTypes.UserRolesChanged,
            SecurityEventTypes.UserPasswordReset, SecurityEventTypes.RuleSetUpdated
        ])
    ];

    public Task<Result<IReadOnlyList<AuditEventTypeGroupDto>>> Handle(
        GetAuditEventTypesQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(Catalog));
}
