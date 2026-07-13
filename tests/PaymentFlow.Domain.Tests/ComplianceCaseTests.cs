using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class ComplianceCaseTests
{
    private static ComplianceCase OpenCase() => new()
    {
        PaymentId = Guid.NewGuid(),
        PaymentReference = "PAY-2026-000099",
        Category = ComplianceCategory.Sanctions,
        Reason = "Watchlist match"
    };

    [Fact]
    public void New_case_is_open_and_blocking()
    {
        var c = OpenCase();
        Assert.Equal(ComplianceCaseStatus.Open, c.Status);
        Assert.True(c.IsBlocking);
    }

    [Fact]
    public void Clear_moves_open_to_cleared_and_stops_blocking()
    {
        var c = OpenCase();
        c.Clear("officer-1", "looks fine", DateTime.UtcNow);

        Assert.Equal(ComplianceCaseStatus.Cleared, c.Status);
        Assert.False(c.IsBlocking);
        Assert.Equal("officer-1", c.ReviewedByUserId);
        Assert.Equal("looks fine", c.ReviewNotes);
        Assert.NotNull(c.ReviewedAtUtc);
    }

    [Fact]
    public void Reject_moves_open_to_rejected_and_keeps_blocking()
    {
        var c = OpenCase();
        c.Reject("officer-1", "confirmed hit", DateTime.UtcNow);

        Assert.Equal(ComplianceCaseStatus.Rejected, c.Status);
        Assert.True(c.IsBlocking); // a rejected hold still blocks settlement
    }

    [Fact]
    public void Clearing_a_cleared_case_throws()
    {
        var c = OpenCase();
        c.Clear("officer-1", null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => c.Clear("officer-2", null, DateTime.UtcNow));
    }

    [Fact]
    public void Rejecting_a_cleared_case_throws()
    {
        var c = OpenCase();
        c.Clear("officer-1", null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => c.Reject("officer-2", null, DateTime.UtcNow));
    }

    [Fact]
    public void Clearing_a_rejected_case_throws()
    {
        var c = OpenCase();
        c.Reject("officer-1", null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => c.Clear("officer-2", null, DateTime.UtcNow));
    }
}
