using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Compliance;

// ---------- Compliance queue ----------

/// <summary>
/// The compliance review queue. Defaults to open cases (the work list); pass a
/// <paramref name="Status"/> to view cleared/rejected history instead.
/// </summary>
public record GetComplianceQueueQuery(ComplianceCaseStatus? Status = ComplianceCaseStatus.Open)
    : IRequest<Result<IReadOnlyList<ComplianceCaseDto>>>;

public sealed class GetComplianceQueueQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetComplianceQueueQuery, Result<IReadOnlyList<ComplianceCaseDto>>>
{
    public async Task<Result<IReadOnlyList<ComplianceCaseDto>>> Handle(
        GetComplianceQueueQuery request, CancellationToken cancellationToken)
    {
        var query = db.ComplianceCases.AsNoTracking();
        if (request.Status is not null)
            query = query.Where(c => c.Status == request.Status);

        // Join to payment for display fields; project to a translatable row, then
        // mask + base64 in memory (those helpers can't run in SQL).
        var rows = await query
            .OrderBy(c => c.CreatedAtUtc)
            .Join(db.Payments.AsNoTracking(), c => c.PaymentId, p => p.Id, (c, p) => new ComplianceRow(
                c.Id, c.PaymentId, c.PaymentReference,
                p.SourceAccountId, p.SourceAccount!.AccountNumber, p.Beneficiary!.Name,
                p.Amount, p.Currency, c.Category, c.Reason, c.RaisedByUserId, c.Status,
                c.ReviewedByUserId, c.ReviewedAtUtc, c.ReviewNotes,
                c.CreatedAtUtc, c.UpdatedAtUtc, c.RowVersion))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ComplianceCaseDto>>(rows.Select(ToDto).ToList());
    }

    internal static ComplianceCaseDto ToDto(ComplianceRow r) =>
        new(r.Id, r.PaymentId, r.PaymentReference, r.SourceAccountId,
            MaskingUtilities.MaskAccountNumber(r.SourceAccountNumber), r.BeneficiaryName,
            r.Amount, r.Currency, r.Category, r.Reason, r.RaisedByUserId, r.Status,
            r.ReviewedByUserId, r.ReviewedAtUtc, r.ReviewNotes,
            r.CreatedAtUtc, r.UpdatedAtUtc, Convert.ToBase64String(r.RowVersion));
}

// ---------- Cases for a single payment ----------

public record GetPaymentComplianceCasesQuery(Guid PaymentId)
    : IRequest<Result<IReadOnlyList<ComplianceCaseDto>>>;

public sealed class GetPaymentComplianceCasesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentComplianceCasesQuery, Result<IReadOnlyList<ComplianceCaseDto>>>
{
    public async Task<Result<IReadOnlyList<ComplianceCaseDto>>> Handle(
        GetPaymentComplianceCasesQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Payments.AsNoTracking()
            .AnyAsync(p => p.Id == request.PaymentId, cancellationToken);
        if (!exists)
            return Result.Failure<IReadOnlyList<ComplianceCaseDto>>(
                Error.NotFound("payment.notFound", "Payment not found."));

        var rows = await db.ComplianceCases.AsNoTracking()
            .Where(c => c.PaymentId == request.PaymentId)
            .OrderBy(c => c.CreatedAtUtc)
            .Join(db.Payments.AsNoTracking(), c => c.PaymentId, p => p.Id, (c, p) => new ComplianceRow(
                c.Id, c.PaymentId, c.PaymentReference,
                p.SourceAccountId, p.SourceAccount!.AccountNumber, p.Beneficiary!.Name,
                p.Amount, p.Currency, c.Category, c.Reason, c.RaisedByUserId, c.Status,
                c.ReviewedByUserId, c.ReviewedAtUtc, c.ReviewNotes,
                c.CreatedAtUtc, c.UpdatedAtUtc, c.RowVersion))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ComplianceCaseDto>>(
            rows.Select(GetComplianceQueueQueryHandler.ToDto).ToList());
    }
}

/// <summary>DB-translatable projection row; masked + base64-encoded in memory.</summary>
internal sealed record ComplianceRow(
    Guid Id, Guid PaymentId, string PaymentReference,
    Guid SourceAccountId, string SourceAccountNumber, string BeneficiaryName,
    decimal Amount, string Currency, ComplianceCategory Category, string Reason,
    string? RaisedByUserId, ComplianceCaseStatus Status,
    string? ReviewedByUserId, DateTime? ReviewedAtUtc, string? ReviewNotes,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, byte[] RowVersion);
