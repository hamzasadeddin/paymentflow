using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

public record PaymentDto(
    Guid Id,
    string PaymentReference,
    Guid SourceAccountId,
    string SourceAccountMaskedNumber,
    Guid BeneficiaryId,
    string BeneficiaryName,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? Description,
    string? CreatedByUserId,
    int RequiredApprovals,
    string? ReviewNotes,
    DateTime? ReviewedAtUtc,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion);

public record CreatePaymentRequest(
    Guid SourceAccountId,
    Guid BeneficiaryId,
    decimal Amount,
    string Currency,
    string? Description);

public record ReviewPaymentRequest(string? Notes);

internal static class PaymentMapping
{
    /// <summary>
    /// Maps an entity that has SourceAccount + Beneficiary loaded. Callers that
    /// project in the database use the DTO constructor directly instead.
    /// </summary>
    public static PaymentDto ToDto(this Payment p) =>
        new(p.Id, p.PaymentReference, p.SourceAccountId,
            p.SourceAccount?.MaskedNumber ?? string.Empty,
            p.BeneficiaryId, p.Beneficiary?.Name ?? string.Empty,
            p.Amount, p.Currency, p.Status, p.Description,
            p.CreatedByUserId, p.RequiredApprovals, p.ReviewNotes, p.ReviewedAtUtc,
            p.FailureReason, p.CreatedAtUtc, p.UpdatedAtUtc,
            Convert.ToBase64String(p.RowVersion));
}
