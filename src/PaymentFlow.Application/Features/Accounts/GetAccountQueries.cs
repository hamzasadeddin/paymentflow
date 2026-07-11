using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Features.Customers;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Accounts;

public record GetAccountByIdQuery(Guid AccountId) : IRequest<Result<AccountSummaryDto>>;

public sealed class GetAccountByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountByIdQuery, Result<AccountSummaryDto>>
{
    public async Task<Result<AccountSummaryDto>> Handle(
        GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await db.PaymentAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

        return account is null
            ? Result.Failure<AccountSummaryDto>(Error.NotFound("account.notFound", "Account not found."))
            : Result.Success(account.ToSummary());
    }
}

public record GetCustomerAccountsQuery(Guid CustomerId) : IRequest<Result<IReadOnlyList<AccountSummaryDto>>>;

public sealed class GetCustomerAccountsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustomerAccountsQuery, Result<IReadOnlyList<AccountSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<AccountSummaryDto>>> Handle(
        GetCustomerAccountsQuery request, CancellationToken cancellationToken)
    {
        var customerExists = await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
            return Result.Failure<IReadOnlyList<AccountSummaryDto>>(
                Error.NotFound("customer.notFound", "Customer not found."));

        var accounts = await db.PaymentAccounts.AsNoTracking()
            .Where(a => a.CustomerId == request.CustomerId)
            .OrderBy(a => a.Currency)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AccountSummaryDto>>(
            accounts.Select(a => a.ToSummary()).ToList());
    }
}

/// <summary>
/// Reveals the full account number. Restricted to privileged roles at the API
/// layer and always writes a security audit event (handled here so it cannot
/// be forgotten by a caller).
/// </summary>
public record RevealAccountNumberQuery(Guid AccountId, Guid? RequestedByUserId, string? IpAddress)
    : IRequest<Result<string>>;

public sealed class RevealAccountNumberQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<RevealAccountNumberQuery, Result<string>>
{
    public async Task<Result<string>> Handle(
        RevealAccountNumberQuery request, CancellationToken cancellationToken)
    {
        var account = await db.PaymentAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

        if (account is null)
            return Result.Failure<string>(Error.NotFound("account.notFound", "Account not found."));

        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            EventType = "AccountNumberRevealed",
            Succeeded = true,
            UserId = request.RequestedByUserId,
            IpAddress = request.IpAddress,
            Details = $"AccountId={account.Id}",
            OccurredAtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(account.AccountNumber);
    }
}
