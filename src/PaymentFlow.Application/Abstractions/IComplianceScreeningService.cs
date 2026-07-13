using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Abstractions;

/// <summary>The outcome of screening a payment against sanctions/limit rules.</summary>
/// <param name="Flagged">True to raise a compliance hold; false to let it through.</param>
/// <param name="Category">Why it flagged (meaningful only when <paramref name="Flagged"/> is true).</param>
/// <param name="Reason">Human-readable explanation, set only when flagged.</param>
public sealed record ScreeningResult(bool Flagged, ComplianceCategory Category, string Reason)
{
    public static ScreeningResult Clear() => new(false, ComplianceCategory.Manual, string.Empty);

    public static ScreeningResult Flag(ComplianceCategory category, string reason) =>
        new(true, category, reason);
}

/// <summary>
/// Decides whether a payment must be held for compliance review. The decision is
/// a pure function of the payment + beneficiary (no randomness), so demo behaviour
/// is reproducible. The concrete rule lives in Infrastructure and is swappable
/// without touching the command handlers — mirroring <see cref="ISettlementSimulator"/>.
/// </summary>
public interface IComplianceScreeningService
{
    ScreeningResult Screen(Payment payment, Beneficiary beneficiary);
}
