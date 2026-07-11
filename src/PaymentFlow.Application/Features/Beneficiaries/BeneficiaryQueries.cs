using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Beneficiaries;

public record GetBeneficiariesQuery(PagedRequest Paging, Guid? CustomerId, BeneficiaryStatus? Status)
    : IRequest<Result<PagedResult<BeneficiaryDto>>>;

public sealed class GetBeneficiariesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBeneficiariesQuery, Result<PagedResult<BeneficiaryDto>>>
{
    public async Task<Result<PagedResult<BeneficiaryDto>>> Handle(
        GetBeneficiariesQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Paging;
        var query = db.Beneficiaries.AsNoTracking();

        if (request.CustomerId is not null)
            query = query.Where(b => b.CustomerId == request.CustomerId);
        if (request.Status is not null)
            query = query.Where(b => b.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var term = paging.Search.Trim();
            query = query.Where(b => b.Name.Contains(term) || (b.BankName != null && b.BankName.Contains(term)));
        }

        query = (paging.SortBy?.ToLowerInvariant()) switch
        {
            "name" => paging.SortDescending ? query.OrderByDescending(b => b.Name) : query.OrderBy(b => b.Name),
            "status" => paging.SortDescending ? query.OrderByDescending(b => b.Status) : query.OrderBy(b => b.Status),
            _ => paging.SortDescending ? query.OrderByDescending(b => b.CreatedAtUtc) : query.OrderBy(b => b.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BeneficiaryDto>(
            items.Select(b => b.ToDto()).ToList(), paging.Page, paging.PageSize, totalCount));
    }
}

public record GetBeneficiaryByIdQuery(Guid BeneficiaryId) : IRequest<Result<BeneficiaryDto>>;

public sealed class GetBeneficiaryByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBeneficiaryByIdQuery, Result<BeneficiaryDto>>
{
    public async Task<Result<BeneficiaryDto>> Handle(
        GetBeneficiaryByIdQuery request, CancellationToken cancellationToken)
    {
        var beneficiary = await db.Beneficiaries.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BeneficiaryId, cancellationToken);

        return beneficiary is null
            ? Result.Failure<BeneficiaryDto>(Error.NotFound("beneficiary.notFound", "Beneficiary not found."))
            : Result.Success(beneficiary.ToDto());
    }
}
