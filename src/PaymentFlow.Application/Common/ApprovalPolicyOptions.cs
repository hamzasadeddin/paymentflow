namespace PaymentFlow.Application.Common;

/// <summary>
/// Approval thresholds, bound from the <c>ApprovalPolicy</c> configuration
/// section. Amounts are compared in the payment's own currency (a deliberate
/// demo simplification, consistent with the no-FX stance elsewhere). Phase 07
/// makes these admin-editable via the rule-settings store, with these
/// config-bound values as the fallback.
/// </summary>
public sealed class ApprovalPolicyOptions
{
    public const string SectionName = "ApprovalPolicy";

    /// <summary>Payments strictly below this amount clear on submit with no checker.</summary>
    public decimal AutoApproveBelow { get; init; } = 1000m;

    /// <summary>Payments at or above this amount require two distinct approvers.</summary>
    public decimal DualApprovalAtOrAbove { get; init; } = 5000m;

    /// <summary>The built-in defaults, used by the demo seeder.</summary>
    public static ApprovalPolicyOptions Defaults => new();
}
