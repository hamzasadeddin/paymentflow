using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Abstractions;

/// <summary>The simulated outcome of settling a payment.</summary>
/// <param name="Succeeds">True to complete the payment; false to fail it.</param>
/// <param name="FailureReason">Human-readable reason, set only when <paramref name="Succeeds"/> is false.</param>
public sealed record SettlementDecision(bool Succeeds, string? FailureReason)
{
    public static SettlementDecision Success() => new(true, null);
    public static SettlementDecision Failure(string reason) => new(false, reason);
}

/// <summary>
/// Decides whether a payment completes or fails during simulated settlement.
/// The decision is a pure function of the payment (no randomness), so failures
/// are reproducible on demand. The concrete rule lives in Infrastructure and is
/// swappable without touching the worker or the command handler.
/// </summary>
public interface ISettlementSimulator
{
    SettlementDecision Decide(Payment payment);
}
