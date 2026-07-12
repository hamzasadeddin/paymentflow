using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Domain.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

public record GetPaymentsQuery(
    PagedRequest Paging,
    PaymentStatus? Status,
    Guid? SourceAccountId,
    Guid? BeneficiaryId,
    DateTime? FromUtc,
    DateTime? ToUtc)
    : IRequest<Result<PagedResult<PaymentDto>>>;

public sealed class GetPaymentsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentsQuery, Result<PagedResult<PaymentDto>>>
{
    public async Task<Result<PagedResult<PaymentDto>>> Handle(
        GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Paging;
        var query = db.Payments.AsNoTracking();

        if (request.Status is not null)
            query = query.Where(p => p.Status == request.Status);
        if (request.SourceAccountId is not null)
            query = query.Where(p => p.SourceAccountId == request.SourceAccountId);
        if (request.BeneficiaryId is not null)
            query = query.Where(p => p.BeneficiaryId == request.BeneficiaryId);
        if (request.FromUtc is not null)
            query = query.Where(p => p.CreatedAtUtc >= request.FromUtc);
        if (request.ToUtc is not null)
            query = query.Where(p => p.CreatedAtUtc <= request.ToUtc);

        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var term = paging.Search.Trim();
            query = query.Where(p =>
                p.PaymentReference.Contains(term) ||
                (p.Description != null && p.Description.Contains(term)) ||
                p.Beneficiary!.Name.Contains(term));
        }

        query = (paging.SortBy?.ToLowerInvariant()) switch
        {
            "amount" => paging.SortDescending ? query.OrderByDescending(p => p.Amount) : query.OrderBy(p => p.Amount),
            "status" => paging.SortDescending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "reference" => paging.SortDescending
                ? query.OrderByDescending(p => p.PaymentReference)
                : query.OrderBy(p => p.PaymentReference),
            _ => paging.SortDescending
                ? query.OrderByDescending(p => p.CreatedAtUtc)
                : query.OrderBy(p => p.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        // Project to a translatable shape (raw account number + rowversion bytes),
        // then mask + base64 in memory since those helpers can't run in SQL.
        var rows = await query
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(p => new PaymentRow(
                p.Id, p.PaymentReference, p.SourceAccountId, p.SourceAccount!.AccountNumber,
                p.BeneficiaryId, p.Beneficiary!.Name, p.Amount, p.Currency, p.Status,
                p.Description, p.CreatedByUserId, p.RequiredApprovals,
                p.ReviewNotes, p.ReviewedAtUtc, p.FailureReason,
                p.CreatedAtUtc, p.UpdatedAtUtc, p.RowVersion))
            .ToListAsync(cancellationToken);

        var items = rows.Select(ToDto).ToList();
        return Result.Success(new PagedResult<PaymentDto>(items, paging.Page, paging.PageSize, totalCount));
    }

    internal static PaymentDto ToDto(PaymentRow r) =>
        new(r.Id, r.PaymentReference, r.SourceAccountId,
            MaskingUtilities.MaskAccountNumber(r.SourceAccountNumber),
            r.BeneficiaryId, r.BeneficiaryName, r.Amount, r.Currency, r.Status,
            r.Description, r.CreatedByUserId, r.RequiredApprovals,
            r.ReviewNotes, r.ReviewedAtUtc, r.FailureReason,
            r.CreatedAtUtc, r.UpdatedAtUtc, Convert.ToBase64String(r.RowVersion));
}

public record GetPaymentByIdQuery(Guid PaymentId) : IRequest<Result<PaymentDto>>;

public sealed class GetPaymentByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentByIdQuery, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var row = await db.Payments.AsNoTracking()
            .Where(p => p.Id == request.PaymentId)
            .Select(p => new PaymentRow(
                p.Id, p.PaymentReference, p.SourceAccountId, p.SourceAccount!.AccountNumber,
                p.BeneficiaryId, p.Beneficiary!.Name, p.Amount, p.Currency, p.Status,
                p.Description, p.CreatedByUserId, p.RequiredApprovals,
                p.ReviewNotes, p.ReviewedAtUtc, p.FailureReason,
                p.CreatedAtUtc, p.UpdatedAtUtc, p.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."))
            : Result.Success(GetPaymentsQueryHandler.ToDto(row));
    }
}

/// <summary>DB-translatable projection row; masked + base64-encoded in memory.</summary>
internal sealed record PaymentRow(
    Guid Id, string PaymentReference, Guid SourceAccountId, string SourceAccountNumber,
    Guid BeneficiaryId, string BeneficiaryName, decimal Amount, string Currency,
    PaymentStatus Status, string? Description, string? CreatedByUserId, int RequiredApprovals,
    string? ReviewNotes, DateTime? ReviewedAtUtc,
    string? FailureReason, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, byte[] RowVersion);
