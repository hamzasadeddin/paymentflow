using PaymentFlow.Application.Common;

namespace PaymentFlow.Application.Features.Admin;

/// <summary>
/// The effective values for one rule section plus its override metadata.
/// <see cref="IsOverridden"/> is false when the section is using the built-in
/// <c>appsettings</c> defaults (no stored row); in that case the metadata fields
/// are null and <see cref="RowVersion"/> is null (nothing to concurrency-check).
/// </summary>
public record RuleSetDto<T>(
    T Values,
    bool IsOverridden,
    string? UpdatedByUserId,
    DateTime? UpdatedAtUtc,
    string? RowVersion);

/// <summary>All four config-backed rule sections, resolved to their effective values.</summary>
public record RulesDto(
    RuleSetDto<ApprovalPolicyOptions> Approval,
    RuleSetDto<ScreeningOptions> Screening,
    RuleSetDto<ReconciliationOptions> Reconciliation,
    RuleSetDto<ProcessingOptions> Processing);

// ---------- API request bodies (per section) ----------

public record UpdateApprovalRulesRequest(
    decimal AutoApproveBelow, decimal DualApprovalAtOrAbove, string? RowVersion);

public record UpdateScreeningRulesRequest(
    bool AutoScreenOnSubmit, string[] WatchlistBeneficiaryNames, string[] WatchlistCountryCodes,
    decimal SinglePaymentReviewLimit, string? RowVersion);

public record UpdateReconciliationRulesRequest(
    bool IntroduceSyntheticBreaks, string DropReferenceEndingIn, decimal PhantomAmount,
    int AmountDriftMinorUnits, string? RowVersion);

public record UpdateProcessingRulesRequest(
    bool AutoProcessEnabled, int PollingIntervalSeconds, int BatchSize,
    int SimulatedLatencyMsMin, int SimulatedLatencyMsMax, int FailOnCents, string? RowVersion);
