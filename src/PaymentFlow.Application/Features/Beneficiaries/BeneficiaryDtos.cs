using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Beneficiaries;

public record BeneficiaryDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    string MaskedNumber,
    string? BankName,
    string? BankIdentifierCode,
    string Currency,
    string? CountryCode,
    BeneficiaryStatus Status,
    string? ReviewNotes,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion);

public record CreateBeneficiaryRequest(
    Guid CustomerId, string Name, string AccountNumber, string? BankName,
    string? BankIdentifierCode, string Currency, string? CountryCode);

public record UpdateBeneficiaryRequest(
    string Name, string AccountNumber, string? BankName,
    string? BankIdentifierCode, string Currency, string? CountryCode, string RowVersion);

public record ReviewBeneficiaryRequest(string? Notes);

internal static class BeneficiaryMapping
{
    public static BeneficiaryDto ToDto(this Beneficiary b) =>
        new(b.Id, b.CustomerId, b.Name, b.MaskedNumber, b.BankName, b.BankIdentifierCode,
            b.Currency, b.CountryCode, b.Status, b.ReviewNotes, b.ReviewedAtUtc,
            b.CreatedAtUtc, b.UpdatedAtUtc, Convert.ToBase64String(b.RowVersion));
}
