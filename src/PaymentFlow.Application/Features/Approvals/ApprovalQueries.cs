using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Approvals;

// ---------- Unified approval queue ----------
public record GetApprovalQueueQuery : IRequest<Result<ApprovalQueueDto>>;

public sealed class GetApprovalQueueQueryHandler(IApplicationDbContext db, IUserLookupService users)
    : IRequestHandler<GetApprovalQueueQuery, Result<ApprovalQueueDto>>
{
    public async Task<Result<ApprovalQueueDto>> Handle(
        GetApprovalQueueQuery request, CancellationToken cancellationToken)
    {
        // Pending payments, projected to a translatable shape first.
        var pendingPayments = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.PendingApproval)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => new
            {
                p.Id,
                p.PaymentReference,
                p.Amount,
                p.Currency,
                BeneficiaryName = p.Beneficiary!.Name,
                p.CreatedByUserId,
                p.RequiredApprovals,
                p.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var paymentIds = pendingPayments.Select(p => p.Id).ToList();

        // Distinct approver count per pending payment (dual-control progress).
        var approvalRows = await db.ApprovalDecisions.AsNoTracking()
            .Where(d => d.SubjectType == ApprovalSubjectType.Payment
                        && d.Decision == ApprovalOutcome.Approved
                        && paymentIds.Contains(d.SubjectId))
            .Select(d => new { d.SubjectId, d.ApproverUserId })
            .ToListAsync(cancellationToken);

        var receivedBySubject = approvalRows
            .GroupBy(r => r.SubjectId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.ApproverUserId).Distinct(StringComparer.Ordinal).Count());

        // Pending beneficiaries (single approval; no dual-control band).
        var pendingBeneficiaries = await db.Beneficiaries.AsNoTracking()
            .Where(b => b.Status == BeneficiaryStatus.PendingApproval)
            .OrderBy(b => b.CreatedAtUtc)
            .Select(b => new { b.Id, b.Name, b.Currency, b.CreatedByUserId, b.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        // Resolve maker ids to emails for both lists in one lookup.
        var makerIds = pendingPayments.Select(p => p.CreatedByUserId)
            .Concat(pendingBeneficiaries.Select(b => b.CreatedByUserId))
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!);
        var makerEmails = await users.GetEmailsByIdsAsync(makerIds, cancellationToken);

        string? EmailFor(string? id) =>
            id is not null && makerEmails.TryGetValue(id, out var email) ? email : null;

        var paymentItems = pendingPayments.Select(p => new ApprovalQueueItemDto(
            ApprovalSubjectType.Payment,
            p.Id,
            p.PaymentReference,
            $"{p.Amount:0.00} {p.Currency} to {p.BeneficiaryName}",
            p.Amount,
            p.Currency,
            p.CreatedByUserId,
            EmailFor(p.CreatedByUserId),
            Math.Max(p.RequiredApprovals, 1),
            receivedBySubject.GetValueOrDefault(p.Id, 0),
            p.CreatedAtUtc)).ToList();

        var beneficiaryItems = pendingBeneficiaries.Select(b => new ApprovalQueueItemDto(
            ApprovalSubjectType.Beneficiary,
            b.Id,
            b.Name,
            $"Beneficiary {b.Name}",
            null,
            b.Currency,
            b.CreatedByUserId,
            EmailFor(b.CreatedByUserId),
            1,
            0,
            b.CreatedAtUtc)).ToList();

        return Result.Success(new ApprovalQueueDto(paymentItems, beneficiaryItems));
    }
}

// ---------- Decision history for a payment ----------
public record GetPaymentApprovalsQuery(Guid PaymentId)
    : IRequest<Result<IReadOnlyList<ApprovalDecisionDto>>>;

public sealed class GetPaymentApprovalsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentApprovalsQuery, Result<IReadOnlyList<ApprovalDecisionDto>>>
{
    public async Task<Result<IReadOnlyList<ApprovalDecisionDto>>> Handle(
        GetPaymentApprovalsQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Payments.AsNoTracking()
            .AnyAsync(p => p.Id == request.PaymentId, cancellationToken);
        if (!exists)
            return Result.Failure<IReadOnlyList<ApprovalDecisionDto>>(
                Error.NotFound("payment.notFound", "Payment not found."));

        var decisions = await db.ApprovalDecisions.AsNoTracking()
            .Where(d => d.SubjectType == ApprovalSubjectType.Payment && d.SubjectId == request.PaymentId)
            .OrderBy(d => d.DecidedAtUtc)
            .Select(d => new ApprovalDecisionDto(
                d.Id, d.ApproverUserId, d.ApproverEmail, d.Decision, d.Notes, d.DecidedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ApprovalDecisionDto>>(decisions);
    }
}
