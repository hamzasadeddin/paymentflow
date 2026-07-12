using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;

namespace PaymentFlow.Infrastructure.Approvals;

/// <summary>
/// Config-backed <see cref="IApprovalPolicyProvider"/>. The pure resolution rule
/// is exposed as a static so the demo seeder can reuse it with the defaults.
/// </summary>
public sealed class ApprovalPolicyProvider(IOptions<ApprovalPolicyOptions> options) : IApprovalPolicyProvider
{
    private readonly ApprovalPolicyOptions _options = options.Value;

    public ApprovalRequirement Resolve(decimal amount)
        => new(RequiredApprovalsFor(amount, _options));

    /// <summary>
    /// Below <see cref="ApprovalPolicyOptions.AutoApproveBelow"/> → 0 (auto);
    /// at/above <see cref="ApprovalPolicyOptions.DualApprovalAtOrAbove"/> → 2;
    /// otherwise → 1.
    /// </summary>
    public static int RequiredApprovalsFor(decimal amount, ApprovalPolicyOptions options)
    {
        if (amount < options.AutoApproveBelow) return 0;
        if (amount >= options.DualApprovalAtOrAbove) return 2;
        return 1;
    }
}
