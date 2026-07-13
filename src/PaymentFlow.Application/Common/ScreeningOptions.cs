namespace PaymentFlow.Application.Common;

/// <summary>
/// Compliance-screening settings, bound from the <c>Compliance</c> configuration
/// section. A payment flags when its beneficiary matches a watchlisted name or
/// country, or when its amount reaches <see cref="SinglePaymentReviewLimit"/>.
/// Phase 06 keeps these in configuration; Phase 07 could move them into an
/// admin-editable store with no change to the screening engine.
/// </summary>
public sealed class ScreeningOptions
{
    public const string SectionName = "Compliance";

    /// <summary>Master switch for automatic screening at submit time.</summary>
    public bool AutoScreenOnSubmit { get; init; } = true;

    /// <summary>Beneficiary names containing any of these terms flag as <c>Sanctions</c> (case-insensitive).</summary>
    public string[] WatchlistBeneficiaryNames { get; init; } = [];

    /// <summary>Beneficiary country codes that flag as <c>Sanctions</c> (case-insensitive, ISO-3166 alpha-2).</summary>
    public string[] WatchlistCountryCodes { get; init; } = [];

    /// <summary>Payments at or above this amount flag as <c>Limit</c> for manual review. 0 disables the limit rule.</summary>
    public decimal SinglePaymentReviewLimit { get; init; } = 5000m;

    /// <summary>The built-in defaults.</summary>
    public static ScreeningOptions Defaults => new();
}
