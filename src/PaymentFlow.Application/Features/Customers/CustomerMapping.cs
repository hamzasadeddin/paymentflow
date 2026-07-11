using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Customers;

internal static class CustomerMapping
{
    public static string Version(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[] ParseVersion(string rowVersion) => Convert.FromBase64String(rowVersion);

    public static CustomerSummaryDto ToSummary(this Customer c, int accountCount) =>
        new(c.Id, c.CustomerReference, c.Name, c.Type, c.Status, c.Email, c.CountryCode,
            accountCount, Version(c.RowVersion));

    public static AccountSummaryDto ToSummary(this PaymentAccount a) =>
        new(a.Id, a.MaskedNumber, a.Currency, a.Status, a.AvailableBalance, a.LedgerBalance,
            a.DailyLimit, Version(a.RowVersion));

    public static CustomerDetailDto ToDetail(this Customer c) =>
        new(c.Id, c.CustomerReference, c.Name, c.Type, c.Status, c.Email, c.PhoneNumber,
            c.CountryCode, c.CreatedAtUtc, c.UpdatedAtUtc, Version(c.RowVersion),
            c.Accounts.Select(a => a.ToSummary()).ToList());
}
