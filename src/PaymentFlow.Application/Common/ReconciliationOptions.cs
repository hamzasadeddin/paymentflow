namespace PaymentFlow.Application.Common;

/// <summary>
/// Reconciliation settings, bound from the <c>Reconciliation</c> configuration
/// section. The simulated statement mirrors the completed payments, then (when
/// <see cref="IntroduceSyntheticBreaks"/> is on) applies a deterministic drift so
/// each run reproducibly yields one of each break type for the demo. Turn the
/// drift off for a clean, zero-break reconciliation.
/// </summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    /// <summary>When true, perturb the statement so breaks appear; when false, it mirrors the ledger exactly.</summary>
    public bool IntroduceSyntheticBreaks { get; init; } = true;

    /// <summary>
    /// The completed payment whose reference ends in this digit is dropped from
    /// the statement (→ <c>MissingFromStatement</c>). Empty disables this drift.
    /// </summary>
    public string DropReferenceEndingIn { get; init; } = "4";

    /// <summary>A phantom statement line of this amount with no matching payment (→ <c>MissingFromLedger</c>). 0 disables it.</summary>
    public decimal PhantomAmount { get; init; } = 999.00m;

    /// <summary>One statement line's amount is bumped by this many minor units (cents) (→ <c>AmountMismatch</c>). 0 disables it.</summary>
    public int AmountDriftMinorUnits { get; init; } = 50;

    /// <summary>The built-in defaults.</summary>
    public static ReconciliationOptions Defaults => new();
}
