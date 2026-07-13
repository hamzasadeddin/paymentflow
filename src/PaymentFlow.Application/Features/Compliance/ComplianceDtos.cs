using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Compliance;

/// <summary>
/// A compliance hold on a payment, with enough denormalized payment detail to
/// render the review row (and the source-account id so the screen can offer the
/// role-gated account-number reveal).
/// </summary>
public record ComplianceCaseDto(
    Guid Id,
    Guid PaymentId,
    string PaymentReference,
    Guid SourceAccountId,
    string SourceAccountMaskedNumber,
    string BeneficiaryName,
    decimal Amount,
    string Currency,
    ComplianceCategory Category,
    string Reason,
    string? RaisedByUserId,
    ComplianceCaseStatus Status,
    string? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewNotes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion);

/// <summary>Notes carried on a clear/reject decision.</summary>
public record ReviewComplianceCaseRequest(string? Notes);
