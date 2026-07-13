using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Reconciliation;

// ---------- Runs (most recent first) ----------

public record GetReconciliationRunsQuery : IRequest<Result<IReadOnlyList<ReconciliationRunDto>>>;

public sealed class GetReconciliationRunsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetReconciliationRunsQuery, Result<IReadOnlyList<ReconciliationRunDto>>>
{
    public async Task<Result<IReadOnlyList<ReconciliationRunDto>>> Handle(
        GetReconciliationRunsQuery request, CancellationToken cancellationToken)
    {
        var runs = await db.ReconciliationRuns.AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReconciliationRunDto(
                r.Id, r.RunReference, r.StatementDateUtc, r.RunByUserId,
                r.MatchedCount, r.BreakCount, r.CompletedAtUtc, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReconciliationRunDto>>(runs);
    }
}

// ---------- Breaks for a run ----------

/// <summary>Breaks for a run; defaults to open breaks, pass <paramref name="Status"/> for history.</summary>
public record GetRunBreaksQuery(Guid RunId, BreakStatus? Status = null)
    : IRequest<Result<IReadOnlyList<ReconciliationBreakDto>>>;

public sealed class GetRunBreaksQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRunBreaksQuery, Result<IReadOnlyList<ReconciliationBreakDto>>>
{
    public async Task<Result<IReadOnlyList<ReconciliationBreakDto>>> Handle(
        GetRunBreaksQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.ReconciliationRuns.AsNoTracking()
            .AnyAsync(r => r.Id == request.RunId, cancellationToken);
        if (!exists)
            return Result.Failure<IReadOnlyList<ReconciliationBreakDto>>(
                Error.NotFound("reconciliation.runNotFound", "Reconciliation run not found."));

        var query = db.ReconciliationBreaks.AsNoTracking()
            .Where(b => b.RunId == request.RunId);
        if (request.Status is not null)
            query = query.Where(b => b.Status == request.Status);

        var breaks = await query
            .OrderBy(b => b.Type)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReconciliationBreakDto>>(
            breaks.Select(ReconciliationMapping.ToDto).ToList());
    }
}

internal static class ReconciliationMapping
{
    public static ReconciliationBreakDto ToDto(ReconciliationBreak b) =>
        new(b.Id, b.RunId, b.Type, b.PaymentId, b.PaymentReference, b.StatementReference,
            b.LedgerAmount, b.StatementAmount, b.Currency, b.Status,
            b.ResolvedByUserId, b.ResolvedAtUtc, b.ResolutionNotes,
            b.CreatedAtUtc, b.UpdatedAtUtc, Convert.ToBase64String(b.RowVersion));
}
