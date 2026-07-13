using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class PaymentProcessingTests
{
    private static Payment ApprovedPayment()
    {
        var p = new Payment
        {
            PaymentReference = "PAY-2026-000042",
            SourceAccountId = Guid.NewGuid(),
            BeneficiaryId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD"
        };
        p.SubmitForApproval(1, DateTime.UtcNow);
        p.Approve("reviewer-1", "ok", DateTime.UtcNow);
        return p;
    }

    private static PaymentAccount ActiveAccount(decimal opening = 1000m) => new()
    {
        AccountNumber = "1234567890123",
        Currency = "USD",
        Status = AccountStatus.Active,
        AvailableBalance = opening,
        LedgerBalance = opening,
        DailyLimit = opening
    };

    [Fact]
    public void MarkProcessing_requires_approved_status()
    {
        var p = new Payment { Amount = 100m };
        Assert.Throws<InvalidOperationException>(() => p.MarkProcessing(DateTime.UtcNow));
    }

    [Fact]
    public void Approved_payment_moves_to_processing()
    {
        var p = ApprovedPayment();
        p.MarkProcessing(DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Processing, p.Status);
    }

    [Fact]
    public void Complete_requires_processing_status()
    {
        var p = ApprovedPayment();
        // Still Approved, not Processing.
        Assert.Throws<InvalidOperationException>(() => p.Complete(DateTime.UtcNow));
    }

    [Fact]
    public void Fail_requires_processing_status()
    {
        var p = ApprovedPayment();
        Assert.Throws<InvalidOperationException>(() => p.Fail("nope", DateTime.UtcNow));
    }

    [Fact]
    public void Complete_moves_processing_to_completed()
    {
        var p = ApprovedPayment();
        p.MarkProcessing(DateTime.UtcNow);
        p.Complete(DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Completed, p.Status);
    }

    [Fact]
    public void Fail_stamps_reason_and_status()
    {
        var p = ApprovedPayment();
        p.MarkProcessing(DateTime.UtcNow);
        p.Fail("Simulated settlement failure", DateTime.UtcNow);

        Assert.Equal(PaymentStatus.Failed, p.Status);
        Assert.Equal("Simulated settlement failure", p.FailureReason);
    }

    [Fact]
    public void Completed_payment_cannot_be_completed_again()
    {
        var p = ApprovedPayment();
        p.MarkProcessing(DateTime.UtcNow);
        p.Complete(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => p.Complete(DateTime.UtcNow));
    }

    // ---------- Account money effects ----------

    [Fact]
    public void Settle_lowers_ledger_and_leaves_available_where_reservation_put_it()
    {
        var account = ActiveAccount(1000m);
        account.Reserve(100m);   // Available 900, Ledger 1000
        account.Settle(100m);    // Available 900, Ledger 900

        Assert.Equal(900m, account.AvailableBalance);
        Assert.Equal(900m, account.LedgerBalance);
    }

    [Fact]
    public void ReleaseReservation_restores_available_and_leaves_ledger_untouched()
    {
        var account = ActiveAccount(1000m);
        account.Reserve(100m);              // Available 900, Ledger 1000
        account.ReleaseReservation(100m);   // Available 1000, Ledger 1000

        Assert.Equal(1000m, account.AvailableBalance);
        Assert.Equal(1000m, account.LedgerBalance);
    }
}
