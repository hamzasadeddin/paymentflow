using MediatR;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;

namespace PaymentFlow.Application.Features.Admin;

/// <summary>Lists users for administration (paged, newest first). Search matches email/display name.</summary>
public record GetUsersQuery(PagedRequest Paging) : IRequest<Result<PagedResult<AdminUserDto>>>;

public sealed class GetUsersQueryHandler(IUserAdminService userAdmin)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<AdminUserDto>>>
{
    public Task<Result<PagedResult<AdminUserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        => userAdmin.ListAsync(request.Paging, cancellationToken);
}
