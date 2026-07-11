using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Application.Features.Customers;

public record GetCustomerByIdQuery(Guid CustomerId) : IRequest<Result<CustomerDetailDto>>;

public sealed class GetCustomerByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDetailDto>>
{
    public async Task<Result<CustomerDetailDto>> Handle(
        GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        return customer is null
            ? Result.Failure<CustomerDetailDto>(Error.NotFound("customer.notFound", "Customer not found."))
            : Result.Success(customer.ToDetail());
    }
}
