using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

public enum AccountStatus { Active = 1, Frozen = 2, Closed = 3 }

public class PaymentAccount : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Stored in full; never serialized directly — use MaskedNumber.</summary>
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    // Money is always decimal, mapped to decimal(19,4). Never float/double.
    public decimal AvailableBalance { get; set; }
    public decimal LedgerBalance { get; set; }
    public decimal DailyLimit { get; set; }

    public string MaskedNumber => MaskingUtilities.MaskAccountNumber(AccountNumber);

    public bool CanDebit(decimal amount) =>
        Status == AccountStatus.Active && amount > 0 && AvailableBalance >= amount;

    /// <summary>
    /// Commits funds for an approved payment: reduces AvailableBalance while
    /// leaving LedgerBalance untouched (the money is spoken for but not yet
    /// settled). Throws if the account cannot debit the amount.
    /// </summary>
    public void Reserve(decimal amount)
    {
        if (!CanDebit(amount))
            throw new InvalidOperationException(
                $"Account {MaskedNumber} cannot reserve {amount} {Currency}.");

        AvailableBalance -= amount;
    }

    /// <summary>Returns a previously reserved amount to AvailableBalance (e.g. on failure).</summary>
    public void ReleaseReservation(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Release amount must be positive.");

        AvailableBalance += amount;
    }

    /// <summary>Settles a completed payment against the ledger position.</summary>
    public void Settle(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Settle amount must be positive.");

        LedgerBalance -= amount;
    }
}
