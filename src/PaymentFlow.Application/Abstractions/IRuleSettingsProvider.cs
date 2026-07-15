namespace PaymentFlow.Application.Abstractions;

/// <summary>
/// Resolves the <b>effective</b> options for a config-backed rule section: the
/// admin-editable override from the rule-settings store when one exists, otherwise
/// the <c>appsettings</c>-bound fallback passed by the caller. This is the single
/// seam that lets the approval, screening, reconciliation, and settlement engines
/// read admin-tuned rules without any change to the engines themselves — they keep
/// depending on their existing provider interfaces, whose implementations consult
/// this instead of <c>IOptions&lt;T&gt;</c> directly.
///
/// The read is synchronous because the engine interfaces it serves
/// (<c>IApprovalPolicyProvider.Resolve</c>, <c>IComplianceScreeningService.Screen</c>,
/// <c>ISettlementSimulator.Decide</c>) are synchronous; it is a single indexed
/// lookup against a tiny table (one row per section).
/// </summary>
public interface IRuleSettingsProvider
{
    /// <summary>
    /// The effective options for <paramref name="section"/>: the stored override
    /// deserialized to <typeparamref name="TOptions"/>, or <paramref name="configFallback"/>
    /// when no override is stored (or it can't be read).
    /// </summary>
    TOptions GetEffective<TOptions>(string section, TOptions configFallback) where TOptions : class;
}
