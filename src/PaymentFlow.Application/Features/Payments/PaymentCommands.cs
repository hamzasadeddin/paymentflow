using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

// ---------- Submit ----------
public record SubmitPaymentCommand(Guid PaymentId) : IRequest<Result<PaymentDto>>;

public sealed class SubmitPaymentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<SubmitPaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        SubmitPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        if (payment.Beneficiary is { Status: not BeneficiaryStatus.Approved })
            return Result.Failure<PaymentDto>(Error.Conflict("payment.beneficiaryNotApproved",
                "The beneficiary must be approved before this payment can be submitted."));

        // Fail-fast funds/limit check (authoritative reservation happens at approve).
        var fundsError = await PaymentGuards.CheckDebitableAsync(
            db, payment.SourceAccount!, payment, clock.UtcNow, cancellationToken);
        if (fundsError is not null)
            return Result.Failure<PaymentDto>(fundsError);

        try
        {
            payment.SubmitForApproval(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(payment.ToDto());
    }
}

// ---------- Cancel ----------
public record CancelPaymentCommand(Guid PaymentId) : IRequest<Result<PaymentDto>>;

public sealed class CancelPaymentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CancelPaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        try
        {
            payment.Cancel(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(payment.ToDto());
    }
}

// ---------- Approve / Reject ----------
public enum PaymentReviewAction { Approve, Reject }

public record TransitionPaymentCommand(
    Guid PaymentId, PaymentReviewAction Action, string? ReviewerUserId, string? Notes)
    : IRequest<Result<PaymentDto>>;

public sealed class TransitionPaymentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<TransitionPaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        TransitionPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        var reviewer = request.ReviewerUserId ?? "system";

        try
        {
            if (request.Action == PaymentReviewAction.Reject)
            {
                payment.Reject(reviewer, request.Notes, clock.UtcNow);
            }
            else
            {
                // Approve reserves funds — re-check the guards at approval time,
                // then debit AvailableBalance before flipping the status.
                if (payment.Beneficiary is { Status: not BeneficiaryStatus.Approved })
                    return Result.Failure<PaymentDto>(Error.Conflict("payment.beneficiaryNotApproved",
                        "The beneficiary must be approved before this payment can be approved."));

                var account = payment.SourceAccount!;
                var fundsError = await PaymentGuards.CheckDebitableAsync(
                    db, account, payment, clock.UtcNow, cancellationToken);
                if (fundsError is not null)
                    return Result.Failure<PaymentDto>(fundsError);

                account.Reserve(payment.Amount);
                account.Touch(clock.UtcNow);
                payment.Approve(reviewer, request.Notes, clock.UtcNow);
            }
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
            return Result.Failure<PaymentDto>(Error.Conflict("payment.concurrencyConflict",
                "The payment or account was modified by someone else. Reload and try again."));
        }

        return Result.Success(payment.ToDto());
    }
}
