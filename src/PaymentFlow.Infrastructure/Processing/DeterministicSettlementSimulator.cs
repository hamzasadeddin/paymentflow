using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Processing;

/// <summary>
/// <see cref="ISettlementSimulator"/> backed by the admin-editable rule store with
/// the <c>appsettings</c>-bound <see cref="ProcessingOptions"/> as the fallback
/// (Phase 07). A payment fails settlement iff the cents component of its amount
/// equals the configured sentinel (default 13) — a pure, reproducible rule that is
/// trivial to trigger in a demo (e.g. 250.13). Because this runs per-payment inside
/// a request scope, an admin change to the failure sentinel takes effect
/// immediately. The pure rule stays a static so it can be reused (e.g. by the demo
/// seeder) with the defaults.
/// </summary>
public sealed class DeterministicSettlementSimulator(
    IRuleSettingsProvider rules,
    Microsoft.Extensions.Options.IOptions<ProcessingOptions> configFallback) : ISettlementSimulator
{
    private readonly ProcessingOptions _configFallback = configFallback.Value;

    public SettlementDecision Decide(Payment payment)
    {
        var options = rules.GetEffective(ProcessingOptions.SectionName, _configFallback);
        return WouldFail(payment.Amount, options.FailOnCents)
            ? SettlementDecision.Failure(
                $"Simulated settlement failure (deterministic rule: cents == {options.FailOnCents}).")
            : SettlementDecision.Success();
    }

    /// <summary>
    /// True when the amount's cents component equals <paramref name="failOnCents"/>.
    /// Uses the absolute value so sign never affects the sentinel comparison.
    /// </summary>
    public static bool WouldFail(decimal amount, int failOnCents)
    {
        var cents = (int)(Math.Abs(amount) * 100m % 100m);
        return cents == failOnCents;
    }
}
