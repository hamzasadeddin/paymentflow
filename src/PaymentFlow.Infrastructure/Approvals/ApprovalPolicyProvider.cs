using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Infrastructure.Approvals;

/// <summary>
/// <see cref="IApprovalPolicyProvider"/> backed by the admin-editable rule store
/// with the <c>appsettings</c>-bound <see cref="ApprovalPolicyOptions"/> as the
/// fallback (Phase 07). The approval engine is unchanged — it still calls
/// <see cref="Resolve"/>; only how the thresholds are fetched changed. The pure
/// resolution rule stays a static so the demo seeder can reuse it with defaults.
/// </summary>
public sealed class ApprovalPolicyProvider(
    IRuleSettingsProvider rules,
    Microsoft.Extensions.Options.IOptions<ApprovalPolicyOptions> configFallback) : IApprovalPolicyProvider
{
    private readonly ApprovalPolicyOptions _configFallback = configFallback.Value;

    public ApprovalRequirement Resolve(decimal amount)
    {
        var options = rules.GetEffective(ApprovalPolicyOptions.SectionName, _configFallback);
        return new(RequiredApprovalsFor(amount, options));
    }

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
