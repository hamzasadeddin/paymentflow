using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Customers;

public record GetCustomersQuery(PagedRequest Paging, CustomerStatus? Status, CustomerType? Type)
    : IRequest<Result<PagedResult<CustomerSummaryDto>>>;

public sealed class GetCustomersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustomersQuery, Result<PagedResult<CustomerSummaryDto>>>
{
    public async Task<Result<PagedResult<CustomerSummaryDto>>> Handle(
        GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Paging;
        var query = db.Customers.AsNoTracking();

        if (request.Status is not null)
            query = query.Where(c => c.Status == request.Status);
        if (request.Type is not null)
            query = query.Where(c => c.Type == request.Type);

        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var term = paging.Search.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                c.CustomerReference.Contains(term) ||
                (c.Email != null && c.Email.Contains(term)));
        }

        query = (paging.SortBy?.ToLowerInvariant()) switch
        {
            "name" => paging.SortDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "status" => paging.SortDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => paging.SortDescending ? query.OrderByDescending(c => c.CreatedAtUtc) : query.OrderBy(c => c.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        // Project to a translatable shape (raw RowVersion bytes + account count),
        // then map to the DTO in memory — Convert.ToBase64String can't run in SQL.
        var rows = await query
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(c => new CustomerRow(
                c.Id, c.CustomerReference, c.Name, c.Type, c.Status, c.Email, c.CountryCode,
                c.Accounts.Count, c.RowVersion))
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new CustomerSummaryDto(
                r.Id, r.CustomerReference, r.Name, r.Type, r.Status, r.Email, r.CountryCode,
                r.AccountCount, Convert.ToBase64String(r.RowVersion)))
            .ToList();

        return Result.Success(new PagedResult<CustomerSummaryDto>(items, paging.Page, paging.PageSize, totalCount));
    }
}

/// <summary>DB-translatable projection row; mapped to the DTO in memory.</summary>
internal sealed record CustomerRow(
    Guid Id, string CustomerReference, string Name, CustomerType Type, CustomerStatus Status,
    string? Email, string? CountryCode, int AccountCount, byte[] RowVersion);
