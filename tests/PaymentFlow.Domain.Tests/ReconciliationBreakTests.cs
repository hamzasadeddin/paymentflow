using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class ReconciliationBreakTests
{
    private static ReconciliationBreak OpenBreak() => new()
    {
        RunId = Guid.NewGuid(),
        Type = BreakType.AmountMismatch,
        PaymentReference = "PAY-2026-000042",
        LedgerAmount = 100m,
        StatementAmount = 100.50m,
        Currency = "USD"
    };

    [Fact]
    public void New_break_is_open()
    {
        Assert.Equal(BreakStatus.Open, OpenBreak().Status);
    }

    [Fact]
    public void Resolve_moves_open_to_resolved_and_stamps()
    {
        var b = OpenBreak();
        b.Resolve("analyst-1", "posted correcting entry", DateTime.UtcNow);

        Assert.Equal(BreakStatus.Resolved, b.Status);
        Assert.Equal("analyst-1", b.ResolvedByUserId);
        Assert.Equal("posted correcting entry", b.ResolutionNotes);
        Assert.NotNull(b.ResolvedAtUtc);
    }

    [Fact]
    public void Ignore_moves_open_to_ignored()
    {
        var b = OpenBreak();
        b.Ignore("analyst-1", "known timing difference", DateTime.UtcNow);
        Assert.Equal(BreakStatus.Ignored, b.Status);
    }

    [Fact]
    public void Resolving_a_resolved_break_throws()
    {
        var b = OpenBreak();
        b.Resolve("analyst-1", null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => b.Resolve("analyst-2", null, DateTime.UtcNow));
    }

    [Fact]
    public void Ignoring_a_resolved_break_throws()
    {
        var b = OpenBreak();
        b.Resolve("analyst-1", null, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => b.Ignore("analyst-2", null, DateTime.UtcNow));
    }
}
