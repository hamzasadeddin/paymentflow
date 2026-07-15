using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Compliance;

/// <summary>
/// <see cref="IComplianceScreeningService"/> backed by the admin-editable rule
/// store with the <c>appsettings</c>-bound <see cref="ScreeningOptions"/> as the
/// fallback (Phase 07). A payment flags when its beneficiary name contains a
/// watchlisted term or its country is watchlisted (→ <see cref="ComplianceCategory.Sanctions"/>),
/// or when its amount reaches the single-payment review limit
/// (→ <see cref="ComplianceCategory.Limit"/>). The rule is pure and deterministic;
/// the command handlers are unchanged. The pure rule stays a static so it can be
/// reused (e.g. by the demo seeder).
/// </summary>
public sealed class RuleBasedComplianceScreeningService(
    IRuleSettingsProvider rules,
    Microsoft.Extensions.Options.IOptions<ScreeningOptions> configFallback) : IComplianceScreeningService
{
    private readonly ScreeningOptions _configFallback = configFallback.Value;

    public ScreeningResult Screen(Payment payment, Beneficiary beneficiary)
    {
        var options = rules.GetEffective(ScreeningOptions.SectionName, _configFallback);
        return Evaluate(beneficiary.Name, beneficiary.CountryCode, payment.Amount, payment.Currency, options);
    }

    /// <summary>The pure screening rule. Sanctions checks take precedence over the limit check.</summary>
    public static ScreeningResult Evaluate(
        string beneficiaryName, string? countryCode, decimal amount, string currency, ScreeningOptions options)
    {
        var name = beneficiaryName ?? string.Empty;
        var matchedName = options.WatchlistBeneficiaryNames
            .FirstOrDefault(term => !string.IsNullOrWhiteSpace(term)
                && name.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (matchedName is not null)
            return ScreeningResult.Flag(ComplianceCategory.Sanctions,
                $"Beneficiary \"{name}\" matched the watchlist term \"{matchedName}\".");

        if (!string.IsNullOrWhiteSpace(countryCode)
            && options.WatchlistCountryCodes.Any(c =>
                string.Equals(c, countryCode, StringComparison.OrdinalIgnoreCase)))
            return ScreeningResult.Flag(ComplianceCategory.Sanctions,
                $"Beneficiary country {countryCode} is on the watchlist.");

        if (options.SinglePaymentReviewLimit > 0 && amount >= options.SinglePaymentReviewLimit)
            return ScreeningResult.Flag(ComplianceCategory.Limit,
                $"Amount {amount:0.00} {currency} is at or above the review limit "
                + $"{options.SinglePaymentReviewLimit:0.00}.");

        return ScreeningResult.Clear();
    }
}
