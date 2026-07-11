using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class BeneficiaryTests
{
    private static Beneficiary NewBeneficiary() => new()
    {
        CustomerId = Guid.NewGuid(),
        Name = "Acme Supplies Ltd",
        AccountNumber = "9988776655",
        Currency = "USD"
    };

    [Fact]
    public void SubmitForApproval_moves_draft_to_pending()
    {
        var b = NewBeneficiary();
        b.SubmitForApproval(DateTime.UtcNow);
        Assert.Equal(BeneficiaryStatus.PendingApproval, b.Status);
    }

    [Fact]
    public void Approve_requires_pending_status()
    {
        var b = NewBeneficiary();
        Assert.Throws<InvalidOperationException>(() => b.Approve("reviewer", null, DateTime.UtcNow));
    }

    [Fact]
    public void Approve_records_reviewer_and_status()
    {
        var b = NewBeneficiary();
        b.SubmitForApproval(DateTime.UtcNow);
        b.Approve("reviewer-1", "looks good", DateTime.UtcNow);

        Assert.Equal(BeneficiaryStatus.Approved, b.Status);
        Assert.Equal("reviewer-1", b.ReviewedByUserId);
        Assert.Equal("looks good", b.ReviewNotes);
        Assert.NotNull(b.ReviewedAtUtc);
    }

    [Fact]
    public void Rejected_beneficiary_can_be_resubmitted()
    {
        var b = NewBeneficiary();
        b.SubmitForApproval(DateTime.UtcNow);
        b.Reject("reviewer-1", "bad IBAN", DateTime.UtcNow);
        Assert.Equal(BeneficiaryStatus.Rejected, b.Status);
        Assert.True(b.CanEdit);

        b.SubmitForApproval(DateTime.UtcNow);
        Assert.Equal(BeneficiaryStatus.PendingApproval, b.Status);
    }

    [Fact]
    public void MaskedNumber_hides_all_but_last_four()
    {
        var b = NewBeneficiary();
        Assert.Equal("******6655", b.MaskedNumber);
    }
}
