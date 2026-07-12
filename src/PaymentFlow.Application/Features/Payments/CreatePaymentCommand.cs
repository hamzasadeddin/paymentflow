using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Constants;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

/// <summary>
/// Creates a payment in <see cref="PaymentStatus.Draft"/>. Idempotent: if a
/// payment already exists for <paramref name="IdempotencyKey"/>, that original
/// payment is returned instead of creating a second one.
/// </summary>
public record CreatePaymentCommand(
    Guid SourceAccountId,
    Guid BeneficiaryId,
    decimal Amount,
    string Currency,
    string? Description,
    string? IdempotencyKey,
    string? CreatedByUserId)
    : IRequest<Result<PaymentDto>>;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.BeneficiaryId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3)
            .Must(Currencies.IsSupported).WithMessage("Unsupported currency code.");
        RuleFor(x => x.Description).MaximumLength(280);
    }
}

public sealed class CreatePaymentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CreatePaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var currency = request.Currency.ToUpperInvariant();
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        // Idempotency replay: same key -> return the original payment.
        var existing = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
            return Result.Success(existing.ToDto());

        var account = await db.PaymentAccounts
            .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, cancellationToken);
        if (account is null)
            return Result.Failure<PaymentDto>(Error.NotFound("account.notFound", "Source account not found."));

        var beneficiary = await db.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == request.BeneficiaryId, cancellationToken);
        if (beneficiary is null)
            return Result.Failure<PaymentDto>(Error.NotFound("beneficiary.notFound", "Beneficiary not found."));

        if (account.Status != AccountStatus.Active)
            return Result.Failure<PaymentDto>(Error.Conflict("payment.accountNotActive",
                $"Source account is {account.Status} and cannot be used."));

        if (beneficiary.Status != BeneficiaryStatus.Approved)
            return Result.Failure<PaymentDto>(Error.Conflict("payment.beneficiaryNotApproved",
                "The beneficiary must be approved before it can be paid."));

        if (!string.Equals(account.Currency, currency, StringComparison.Ordinal))
            return Result.Failure<PaymentDto>(Error.Validation("payment.currencyMismatch",
                $"Payment currency {currency} does not match the source account currency {account.Currency}."));

        if (!string.Equals(beneficiary.Currency, currency, StringComparison.Ordinal))
            return Result.Failure<PaymentDto>(Error.Validation("payment.currencyMismatch",
                $"Payment currency {currency} does not match the beneficiary currency {beneficiary.Currency}."));

        var payment = new Payment
        {
            PaymentReference = await GenerateReferenceAsync(cancellationToken),
            SourceAccountId = account.Id,
            BeneficiaryId = beneficiary.Id,
            Amount = request.Amount,
            Currency = currency,
            Description = request.Description?.Trim(),
            IdempotencyKey = idempotencyKey,
            CreatedByUserId = string.IsNullOrWhiteSpace(request.CreatedByUserId) ? null : request.CreatedByUserId,
            CreatedAtUtc = clock.UtcNow
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        // Attach navigation data for the response without another round-trip.
        payment.SourceAccount = account;
        payment.Beneficiary = beneficiary;
        return Result.Success(payment.ToDto());
    }

    private async Task<string> GenerateReferenceAsync(CancellationToken cancellationToken)
    {
        var year = clock.UtcNow.Year;
        var countThisYear = await db.Payments
            .CountAsync(p => p.CreatedAtUtc.Year == year, cancellationToken);
        return $"PAY-{year}-{(countThisYear + 1):D6}";
    }
}
