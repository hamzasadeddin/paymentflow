using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Reconciliation;

/// <summary>Summary of one reconciliation pass.</summary>
public record ReconciliationRunDto(
    Guid Id,
    string RunReference,
    DateTime StatementDateUtc,
    string? RunByUserId,
    int MatchedCount,
    int BreakCount,
    DateTime CompletedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>A single reconciliation difference, with whichever side(s) exist.</summary>
public record ReconciliationBreakDto(
    Guid Id,
    Guid RunId,
    BreakType Type,
    Guid? PaymentId,
    string? PaymentReference,
    string? StatementReference,
    decimal? LedgerAmount,
    decimal? StatementAmount,
    string Currency,
    BreakStatus Status,
    string? ResolvedByUserId,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion);

/// <summary>Notes carried on a resolve/ignore decision.</summary>
public record ResolveBreakRequest(string? Notes);
