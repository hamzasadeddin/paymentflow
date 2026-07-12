namespace PaymentFlow.Application.Abstractions;

/// <summary>The approval requirement resolved for a given payment amount.</summary>
public sealed record ApprovalRequirement(int RequiredApprovals)
{
    /// <summary>True when the amount is below the approval threshold and clears on submit.</summary>
    public bool AutoApproves => RequiredApprovals == 0;
}

/// <summary>
/// Resolves how many distinct approvers a payment needs, from the configured
/// thresholds. Phase 04 binds this from configuration; Phase 07 will back it
/// with an admin-editable store without changing the approval engine.
/// </summary>
public interface IApprovalPolicyProvider
{
    ApprovalRequirement Resolve(decimal amount);
}
