using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Reconciliation;

/// <summary>
/// Runs a reconciliation pass: settled (<see cref="PaymentStatus.Completed"/>)
/// payments — the ledger side, the source of truth — are matched by reference
/// against the external statement lines. Every difference is persisted as a
/// <see cref="ReconciliationBreak"/> under a new <see cref="ReconciliationRun"/>.
/// On-demand only (reviewer-initiated); there is no background job.
/// </summary>
public record RunReconciliationCommand(string? RunByUserId) : IRequest<Result<ReconciliationRunDto>>;

public sealed class RunReconciliationCommandHandler(
    IApplicationDbContext db, IDateTimeProvider clock, IExternalStatementProvider statements)
    : IRequestHandler<RunReconciliationCommand, Result<ReconciliationRunDto>>
{
    public async Task<Result<ReconciliationRunDto>> Handle(
        RunReconciliationCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // Ledger side: completed payments (reference + amount + currency).
        var ledger = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .Select(p => new { p.Id, p.PaymentReference, p.Amount, p.Currency })
            .ToListAsync(cancellationToken);

        // External side: the simulated statement as of now.
        var statement = await statements.GetStatementAsync(now, cancellationToken);
        var statementByRef = statement
            .GroupBy(s => s.Reference, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var breaks = new List<ReconciliationBreak>();
        var matched = 0;
        var run = new ReconciliationRun
        {
            RunReference = await GenerateReferenceAsync(now, cancellationToken),
            StatementDateUtc = now,
            RunByUserId = string.IsNullOrWhiteSpace(request.RunByUserId) ? null : request.RunByUserId,
            CompletedAtUtc = now,
            CreatedAtUtc = now
        };

        // Ledger → statement: matched, amount-mismatch, or missing-from-statement.
        foreach (var payment in ledger)
        {
            if (statementByRef.TryGetValue(payment.PaymentReference, out var line))
            {
                if (line.Amount == payment.Amount)
                {
                    matched++;
                }
                else
                {
                    breaks.Add(new ReconciliationBreak
                    {
                        RunId = run.Id,
                        Type = BreakType.AmountMismatch,
                        PaymentId = payment.Id,
                        PaymentReference = payment.PaymentReference,
                        StatementReference = line.Reference,
                        LedgerAmount = payment.Amount,
                        StatementAmount = line.Amount,
                        Currency = payment.Currency,
                        CreatedAtUtc = now
                    });
                }
            }
            else
            {
                breaks.Add(new ReconciliationBreak
                {
                    RunId = run.Id,
                    Type = BreakType.MissingFromStatement,
                    PaymentId = payment.Id,
                    PaymentReference = payment.PaymentReference,
                    LedgerAmount = payment.Amount,
                    Currency = payment.Currency,
                    CreatedAtUtc = now
                });
            }
        }

        // Statement → ledger: any statement line with no matching completed payment.
        var ledgerRefs = ledger.Select(p => p.PaymentReference).ToHashSet(StringComparer.Ordinal);
        foreach (var line in statement)
        {
            if (!ledgerRefs.Contains(line.Reference))
            {
                breaks.Add(new ReconciliationBreak
                {
                    RunId = run.Id,
                    Type = BreakType.MissingFromLedger,
                    StatementReference = line.Reference,
                    StatementAmount = line.Amount,
                    Currency = line.Currency,
                    CreatedAtUtc = now
                });
            }
        }

        run.MatchedCount = matched;
        run.BreakCount = breaks.Count;

        db.ReconciliationRuns.Add(run);
        db.ReconciliationBreaks.AddRange(breaks);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = Guid.TryParse(request.RunByUserId, out var id) ? id : null,
            EventType = SecurityEventTypes.ReconciliationRunCompleted,
            Succeeded = true,
            Details = $"{run.RunReference}: {matched} matched, {breaks.Count} break(s).",
            OccurredAtUtc = now
        });

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReconciliationRunDto(
            run.Id, run.RunReference, run.StatementDateUtc, run.RunByUserId,
            run.MatchedCount, run.BreakCount, run.CompletedAtUtc, run.CreatedAtUtc));
    }

    private async Task<string> GenerateReferenceAsync(DateTime now, CancellationToken cancellationToken)
    {
        var year = now.Year;
        var countThisYear = await db.ReconciliationRuns
            .CountAsync(r => r.CreatedAtUtc.Year == year, cancellationToken);
        return $"RECON-{year}-{(countThisYear + 1):D6}";
    }
}
