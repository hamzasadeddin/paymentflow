using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Customers;

public record CustomerSummaryDto(
    Guid Id,
    string CustomerReference,
    string Name,
    CustomerType Type,
    CustomerStatus Status,
    string? Email,
    string? CountryCode,
    int AccountCount,
    string RowVersion);

public record CustomerDetailDto(
    Guid Id,
    string CustomerReference,
    string Name,
    CustomerType Type,
    CustomerStatus Status,
    string? Email,
    string? PhoneNumber,
    string? CountryCode,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<AccountSummaryDto> Accounts);

public record AccountSummaryDto(
    Guid Id,
    string MaskedNumber,
    string Currency,
    AccountStatus Status,
    decimal AvailableBalance,
    decimal LedgerBalance,
    decimal DailyLimit,
    string RowVersion);

public record CreateCustomerRequest(
    CustomerType Type,
    string Name,
    string? Email,
    string? PhoneNumber,
    string? CountryCode);

public record UpdateCustomerRequest(
    string Name,
    string? Email,
    string? PhoneNumber,
    string? CountryCode,
    CustomerStatus Status,
    string RowVersion);
