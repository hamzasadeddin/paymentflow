using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Processing;

/// <summary>
/// Config-backed <see cref="ISettlementSimulator"/>. A payment fails settlement
/// iff the cents component of its amount equals the configured sentinel
/// (default 13) — a pure, reproducible rule that is trivial to trigger in a demo
/// (e.g. an amount of 250.13). The pure rule is exposed as a static so it can be
/// reused (e.g. by the demo seeder) with the defaults.
/// </summary>
public sealed class DeterministicSettlementSimulator(IOptions<ProcessingOptions> options) : ISettlementSimulator
{
    private readonly ProcessingOptions _options = options.Value;

    public SettlementDecision Decide(Payment payment)
        => WouldFail(payment.Amount, _options.FailOnCents)
            ? SettlementDecision.Failure(
                $"Simulated settlement failure (deterministic rule: cents == {_options.FailOnCents}).")
            : SettlementDecision.Success();

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
