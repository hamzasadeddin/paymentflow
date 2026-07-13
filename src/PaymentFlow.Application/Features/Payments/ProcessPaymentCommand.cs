using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

/// <summary>
/// Settles an <see cref="PaymentStatus.Approved"/> payment, driving it through
/// <see cref="PaymentStatus.Processing"/> to either <see cref="PaymentStatus.Completed"/>
/// or <see cref="PaymentStatus.Failed"/>. This one handler is the single source of
/// truth for settlement — both the background worker and the manual endpoint call it.
/// </summary>
public record ProcessPaymentCommand(Guid PaymentId) : IRequest<Result<PaymentDto>>;

public sealed class ProcessPaymentCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider clock,
    ISettlementSimulator simulator,
    IPaymentNotificationService notifier,
    IOptions<ProcessingOptions> options)
    : IRequestHandler<ProcessPaymentCommand, Result<PaymentDto>>
{
    private readonly ProcessingOptions _options = options.Value;

    public async Task<Result<PaymentDto>> Handle(
        ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        // ---- Phase 1: claim the payment by flipping Approved -> Processing. ----
        // This save is the atomic claim: if the worker and the manual endpoint
        // (or two worker ticks) race, only one wins the RowVersion check; the
        // loser is reported as a conflict and does not settle.
        try
        {
            payment.MarkProcessing(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.alreadyProcessing",
                "This payment is already being processed."));
        }

        await notifier.PaymentStatusChangedAsync(ToNotification(payment), cancellationToken);

        // ---- Simulate settlement latency (timing only; never affects the outcome). ----
        await SimulateLatencyAsync(cancellationToken);

        // ---- Phase 2: settle deterministically, then complete or fail. ----
        var account = payment.SourceAccount!;
        var decision = simulator.Decide(payment);

        try
        {
            if (decision.Succeeds)
            {
                account.Settle(payment.Amount);
                account.Touch(clock.UtcNow);
                payment.Complete(clock.UtcNow);
            }
            else
            {
                account.ReleaseReservation(payment.Amount);
                account.Touch(clock.UtcNow);
                payment.Fail(decision.FailureReason ?? "Settlement failed.", clock.UtcNow);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
        }

        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            EventType = decision.Succeeds ? SecurityEventTypes.PaymentCompleted : SecurityEventTypes.PaymentFailed,
            Succeeded = decision.Succeeds,
            Details = decision.Succeeds
                ? $"{payment.PaymentReference} settled ({payment.Amount} {payment.Currency})."
                : $"{payment.PaymentReference} failed settlement: {payment.FailureReason}",
            OccurredAtUtc = clock.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.concurrencyConflict",
                "The payment or account was modified by someone else. Reload and try again."));
        }

        await notifier.PaymentStatusChangedAsync(ToNotification(payment), cancellationToken);

        return Result.Success(payment.ToDto());
    }

    private async Task SimulateLatencyAsync(CancellationToken cancellationToken)
    {
        var max = _options.SimulatedLatencyMsMax;
        if (max <= 0)
            return;

        var min = Math.Max(0, Math.Min(_options.SimulatedLatencyMsMin, max));
        var delay = min == max ? max : Random.Shared.Next(min, max + 1);
        await Task.Delay(delay, cancellationToken);
    }

    private static PaymentStatusChangedNotification ToNotification(Payment payment) =>
        new(payment.Id, payment.PaymentReference, payment.Status,
            payment.FailureReason, payment.UpdatedAtUtc ?? payment.CreatedAtUtc);
}
