using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Reconciliation;

/// <summary>Resolve or ignore a reconciliation break.</summary>
public enum BreakReviewAction { Resolve, Ignore }

/// <summary>
/// Applies an operator's decision to an open break. Resolving records it as
/// worked; ignoring dismisses it as expected/benign. Re-deciding a closed break
/// surfaces as 409, as does a stale RowVersion under a race.
/// </summary>
public record ReviewBreakCommand(
    Guid BreakId,
    BreakReviewAction Action,
    string? ReviewerUserId,
    string? Notes)
    : IRequest<Result<ReconciliationBreakDto>>;

public sealed class ReviewBreakCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ReviewBreakCommand, Result<ReconciliationBreakDto>>
{
    public async Task<Result<ReconciliationBreakDto>> Handle(
        ReviewBreakCommand request, CancellationToken cancellationToken)
    {
        var breakRow = await db.ReconciliationBreaks
            .FirstOrDefaultAsync(b => b.Id == request.BreakId, cancellationToken);

        if (breakRow is null)
            return Result.Failure<ReconciliationBreakDto>(
                Error.NotFound("reconciliation.breakNotFound", "Reconciliation break not found."));

        if (string.IsNullOrWhiteSpace(request.ReviewerUserId))
            return Result.Failure<ReconciliationBreakDto>(Error.Forbidden("reconciliation.unknownReviewer",
                "The reviewer identity could not be determined."));

        var resolved = request.Action == BreakReviewAction.Resolve;

        try
        {
            if (resolved)
                breakRow.Resolve(request.ReviewerUserId, request.Notes, clock.UtcNow);
            else
                breakRow.Ignore(request.ReviewerUserId, request.Notes, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ReconciliationBreakDto>(
                Error.Conflict("reconciliation.invalidTransition", ex.Message));
        }

        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = Guid.TryParse(request.ReviewerUserId, out var id) ? id : null,
            EventType = resolved
                ? SecurityEventTypes.ReconciliationBreakResolved
                : SecurityEventTypes.ReconciliationBreakIgnored,
            Succeeded = true,
            Details = $"Break {breakRow.Id} ({breakRow.Type}) {(resolved ? "resolved" : "ignored")}.",
            OccurredAtUtc = clock.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ReconciliationBreakDto>(Error.Conflict("reconciliation.concurrencyConflict",
                "The break was modified by someone else. Reload and try again."));
        }

        return Result.Success(ReconciliationMapping.ToDto(breakRow));
    }
}
