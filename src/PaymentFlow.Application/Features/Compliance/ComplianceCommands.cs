using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Compliance;

/// <summary>Clear or reject a compliance hold.</summary>
public enum ComplianceReviewAction { Clear, Reject }

/// <summary>
/// Applies a Compliance Officer's decision to an open case. Clearing unblocks
/// settlement (the payment can then process); rejecting keeps the payment blocked
/// permanently (operations cancels it via the normal path). Invalid transitions
/// (deciding an already-closed case) surface as 409; a stale RowVersion under a
/// race also surfaces as 409.
/// </summary>
public record ReviewComplianceCaseCommand(
    Guid CaseId,
    ComplianceReviewAction Action,
    string? ReviewerUserId,
    string? ReviewerEmail,
    string? Notes)
    : IRequest<Result<ComplianceCaseDto>>;

public sealed class ReviewComplianceCaseCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ReviewComplianceCaseCommand, Result<ComplianceCaseDto>>
{
    public async Task<Result<ComplianceCaseDto>> Handle(
        ReviewComplianceCaseCommand request, CancellationToken cancellationToken)
    {
        var complianceCase = await db.ComplianceCases
            .FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken);

        if (complianceCase is null)
            return Result.Failure<ComplianceCaseDto>(
                Error.NotFound("compliance.notFound", "Compliance case not found."));

        if (string.IsNullOrWhiteSpace(request.ReviewerUserId))
            return Result.Failure<ComplianceCaseDto>(Error.Forbidden("compliance.unknownReviewer",
                "The reviewer identity could not be determined."));

        var cleared = request.Action == ComplianceReviewAction.Clear;

        try
        {
            if (cleared)
                complianceCase.Clear(request.ReviewerUserId, request.Notes, clock.UtcNow);
            else
                complianceCase.Reject(request.ReviewerUserId, request.Notes, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ComplianceCaseDto>(Error.Conflict("compliance.invalidTransition", ex.Message));
        }

        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = Guid.TryParse(request.ReviewerUserId, out var id) ? id : null,
            Email = request.ReviewerEmail,
            EventType = cleared ? SecurityEventTypes.ComplianceHoldCleared : SecurityEventTypes.ComplianceHoldRejected,
            Succeeded = true,
            Details = $"{complianceCase.PaymentReference} compliance hold {(cleared ? "cleared" : "rejected")}.",
            OccurredAtUtc = clock.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ComplianceCaseDto>(Error.Conflict("compliance.concurrencyConflict",
                "The compliance case was modified by someone else. Reload and try again."));
        }

        // Reload with payment detail for the response projection.
        return await ReadBackAsync(complianceCase.Id, cancellationToken);
    }

    private async Task<Result<ComplianceCaseDto>> ReadBackAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var dto = await db.ComplianceCases.AsNoTracking()
            .Where(c => c.Id == caseId)
            .Join(db.Payments.AsNoTracking(), c => c.PaymentId, p => p.Id, (c, p) => new ComplianceRow(
                c.Id, c.PaymentId, c.PaymentReference,
                p.SourceAccountId, p.SourceAccount!.AccountNumber, p.Beneficiary!.Name,
                p.Amount, p.Currency, c.Category, c.Reason, c.RaisedByUserId, c.Status,
                c.ReviewedByUserId, c.ReviewedAtUtc, c.ReviewNotes,
                c.CreatedAtUtc, c.UpdatedAtUtc, c.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ComplianceCaseDto>(Error.NotFound("compliance.notFound", "Compliance case not found."))
            : Result.Success(GetComplianceQueueQueryHandler.ToDto(dto));
    }
}
