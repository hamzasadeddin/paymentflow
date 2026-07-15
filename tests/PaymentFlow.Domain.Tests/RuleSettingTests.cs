using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class RuleSettingTests
{
    [Fact]
    public void Apply_sets_value_editor_and_timestamp()
    {
        var setting = new RuleSetting { Section = "ApprovalPolicy" };
        var now = DateTime.UtcNow;

        setting.Apply("{\"autoApproveBelow\":250}", "admin-1", now);

        Assert.Equal("{\"autoApproveBelow\":250}", setting.ValueJson);
        Assert.Equal("admin-1", setting.UpdatedByUserId);
        Assert.Equal(now, setting.UpdatedAtUtc);
    }

    [Fact]
    public void Apply_allows_a_null_editor()
    {
        var setting = new RuleSetting { Section = "Compliance" };
        setting.Apply("{}", null, DateTime.UtcNow);
        Assert.Null(setting.UpdatedByUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Apply_rejects_an_empty_value(string value)
    {
        var setting = new RuleSetting { Section = "Processing" };
        Assert.Throws<InvalidOperationException>(() => setting.Apply(value, "admin-1", DateTime.UtcNow));
    }
}
