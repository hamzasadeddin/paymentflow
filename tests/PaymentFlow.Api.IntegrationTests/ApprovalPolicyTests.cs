using PaymentFlow.Infrastructure.Approvals;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

/// <summary>
/// Pure unit tests for the approval-band resolver (no web host required).
/// Uses the built-in defaults: auto below 1,000; dual at/above 5,000.
/// </summary>
public class ApprovalPolicyTests
{
    private static readonly ApprovalPolicyOptions Defaults = ApprovalPolicyOptions.Defaults;

    [Theory]
    [InlineData(0.01)]
    [InlineData(250)]
    [InlineData(999.99)]
    public void Below_threshold_requires_no_approval(decimal amount)
        => Assert.Equal(0, ApprovalPolicyProvider.RequiredApprovalsFor(amount, Defaults));

    [Theory]
    [InlineData(1000)]
    [InlineData(2500)]
    [InlineData(4999.99)]
    public void Mid_band_requires_one_approval(decimal amount)
        => Assert.Equal(1, ApprovalPolicyProvider.RequiredApprovalsFor(amount, Defaults));

    [Theory]
    [InlineData(5000)]
    [InlineData(25000)]
    public void At_or_above_dual_threshold_requires_two_approvals(decimal amount)
        => Assert.Equal(2, ApprovalPolicyProvider.RequiredApprovalsFor(amount, Defaults));
}
