using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Features.Customers;
using PaymentFlow.Domain.Constants;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Accounts;

public record CreateAccountCommand(
    Guid CustomerId, string Currency, decimal OpeningBalance, decimal DailyLimit)
    : IRequest<Result<AccountSummaryDto>>;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().Length(3)
            .Must(Currencies.IsSupported).WithMessage("Unsupported currency code.");
        RuleFor(x => x.OpeningBalance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DailyLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateAccountCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CreateAccountCommand, Result<AccountSummaryDto>>
{
    public async Task<Result<AccountSummaryDto>> Handle(
        CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var customerExists = await db.Customers
            .AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
            return Result.Failure<AccountSummaryDto>(Error.NotFound("customer.notFound", "Customer not found."));

        var account = new PaymentAccount
        {
            CustomerId = request.CustomerId,
            Currency = request.Currency.ToUpperInvariant(),
            AccountNumber = GenerateAccountNumber(),
            AvailableBalance = request.OpeningBalance,
            LedgerBalance = request.OpeningBalance,
            DailyLimit = request.DailyLimit,
            Status = AccountStatus.Active,
            CreatedAtUtc = clock.UtcNow
        };

        db.PaymentAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(account.ToSummary());
    }

    // Fictional 12-digit account number; last 4 are what the UI shows.
    private static string GenerateAccountNumber() =>
        Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
}
