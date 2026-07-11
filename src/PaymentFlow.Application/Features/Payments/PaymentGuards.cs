using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

/// <summary>
/// Funds-related guards shared by submit and approve. Returns a failure
/// <see cref="Error"/> when a rule is violated, or <c>null</c> when the
/// account can cover the payment.
/// </summary>
internal static class PaymentGuards
{
    /// <summary>
    /// Verifies the source account is active, has the funds available, and that
    /// approving this payment would not breach the account's daily limit.
    /// </summary>
    public static async Task<Error?> CheckDebitableAsync(
        IApplicationDbContext db, PaymentAccount account, Payment payment,
        DateTime utcNow, CancellationToken cancellationToken)
    {
        if (account.Status != AccountStatus.Active)
            return Error.Conflict("payment.accountNotActive",
                $"Source account is {account.Status} and cannot be debited.");

        if (!account.CanDebit(payment.Amount))
            return Error.Conflict("payment.insufficientFunds",
                "Source account has insufficient available funds for this payment.");

        // Daily limit: sum of amounts already committed today from this account
        // (Approved / Processing / Completed) plus this payment must fit the cap.
        // Summed in memory — SQLite (used in tests) can't aggregate decimals server-side.
        var today = utcNow.Date;
        var tomorrow = today.AddDays(1);

        var committedAmounts = await db.Payments
            .Where(p => p.SourceAccountId == account.Id
                        && p.Id != payment.Id
                        && (p.Status == PaymentStatus.Approved
                            || p.Status == PaymentStatus.Processing
                            || p.Status == PaymentStatus.Completed)
                        && p.CreatedAtUtc >= today && p.CreatedAtUtc < tomorrow)
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken);

        var committedToday = committedAmounts.Sum();

        if (committedToday + payment.Amount > account.DailyLimit)
            return Error.Conflict("payment.dailyLimitExceeded",
                $"This payment would exceed the account's daily limit of {account.DailyLimit} {account.Currency}.");

        return null;
    }
}
