using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class PaymentTests
{
    private static Payment NewPayment() => new()
    {
        PaymentReference = "PAY-2026-000001",
        SourceAccountId = Guid.NewGuid(),
        BeneficiaryId = Guid.NewGuid(),
        Amount = 100m,
        Currency = "USD"
    };

    [Fact]
    public void New_payment_starts_as_draft()
        => Assert.Equal(PaymentStatus.Draft, NewPayment().Status);

    [Fact]
    public void Submit_moves_draft_to_pending()
    {
        var p = NewPayment();
        p.SubmitForApproval(1, DateTime.UtcNow);
        Assert.Equal(PaymentStatus.PendingApproval, p.Status);
    }

    [Fact]
    public void Submit_stamps_required_approvals()
    {
        var p = NewPayment();
        p.SubmitForApproval(2, DateTime.UtcNow);
        Assert.Equal(2, p.RequiredApprovals);
    }

    [Fact]
    public void Submit_rejects_negative_required_approvals()
    {
        var p = NewPayment();
        Assert.Throws<InvalidOperationException>(() => p.SubmitForApproval(-1, DateTime.UtcNow));
    }

    [Fact]
    public void Approve_requires_pending_status()
    {
        var p = NewPayment();
        Assert.Throws<InvalidOperationException>(() => p.Approve("reviewer", null, DateTime.UtcNow));
    }

    [Fact]
    public void Approve_records_reviewer()
    {
        var p = NewPayment();
        p.SubmitForApproval(1, DateTime.UtcNow);
        p.Approve("reviewer-1", "cleared", DateTime.UtcNow);

        Assert.Equal(PaymentStatus.Approved, p.Status);
        Assert.Equal("reviewer-1", p.ReviewedByUserId);
        Assert.Equal("cleared", p.ReviewNotes);
        Assert.NotNull(p.ReviewedAtUtc);
    }

    [Fact]
    public void Reject_requires_pending_status()
    {
        var p = NewPayment();
        Assert.Throws<InvalidOperationException>(() => p.Reject("reviewer", "no", DateTime.UtcNow));
    }

    [Fact]
    public void Cancel_allowed_from_draft_and_pending_only()
    {
        var draft = NewPayment();
        draft.Cancel(DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Cancelled, draft.Status);

        var approved = NewPayment();
        approved.SubmitForApproval(1, DateTime.UtcNow);
        approved.Approve("r", null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => approved.Cancel(DateTime.UtcNow));
    }

    [Fact]
    public void Full_happy_path_reaches_completed()
    {
        var p = NewPayment();
        p.SubmitForApproval(1, DateTime.UtcNow);
        p.Approve("r", null, DateTime.UtcNow);
        p.MarkProcessing(DateTime.UtcNow);
        p.Complete(DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Completed, p.Status);
    }

    [Fact]
    public void Fail_only_from_processing_and_records_reason()
    {
        var p = NewPayment();
        Assert.Throws<InvalidOperationException>(() => p.Fail("early", DateTime.UtcNow));

        p.SubmitForApproval(1, DateTime.UtcNow);
        p.Approve("r", null, DateTime.UtcNow);
        p.MarkProcessing(DateTime.UtcNow);
        p.Fail("network timeout", DateTime.UtcNow);

        Assert.Equal(PaymentStatus.Failed, p.Status);
        Assert.Equal("network timeout", p.FailureReason);
    }

    [Fact]
    public void Cannot_process_an_unapproved_payment()
    {
        var p = NewPayment();
        p.SubmitForApproval(1, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => p.MarkProcessing(DateTime.UtcNow));
    }
}

public class PaymentAccountReservationTests
{
    private static PaymentAccount NewAccount(decimal available = 1000m, decimal ledger = 1000m) => new()
    {
        AccountNumber = "123456789012",
        Currency = "USD",
        AvailableBalance = available,
        LedgerBalance = ledger,
        DailyLimit = 10000m,
        Status = AccountStatus.Active
    };

    [Fact]
    public void Reserve_reduces_available_but_not_ledger()
    {
        var account = NewAccount();
        account.Reserve(250m);
        Assert.Equal(750m, account.AvailableBalance);
        Assert.Equal(1000m, account.LedgerBalance);
    }

    [Fact]
    public void Reserve_throws_when_insufficient_funds()
    {
        var account = NewAccount(available: 100m);
        Assert.Throws<InvalidOperationException>(() => account.Reserve(250m));
    }

    [Fact]
    public void Reserve_throws_when_account_not_active()
    {
        var account = NewAccount();
        account.Status = AccountStatus.Frozen;
        Assert.Throws<InvalidOperationException>(() => account.Reserve(50m));
    }

    [Fact]
    public void Release_returns_funds_to_available()
    {
        var account = NewAccount();
        account.Reserve(250m);
        account.ReleaseReservation(250m);
        Assert.Equal(1000m, account.AvailableBalance);
    }

    [Fact]
    public void Settle_reduces_ledger()
    {
        var account = NewAccount();
        account.Reserve(250m);
        account.Settle(250m);
        Assert.Equal(750m, account.AvailableBalance);
        Assert.Equal(750m, account.LedgerBalance);
    }
}
