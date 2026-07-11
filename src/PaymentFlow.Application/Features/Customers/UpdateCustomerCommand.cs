using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Customers;

public record UpdateCustomerCommand(
    Guid CustomerId, string Name, string? Email, string? PhoneNumber,
    string? CountryCode, CustomerStatus Status, string RowVersion)
    : IRequest<Result<CustomerDetailDto>>;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.CountryCode).Length(2).When(x => !string.IsNullOrWhiteSpace(x.CountryCode));
    }
}

public sealed class UpdateCustomerCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UpdateCustomerCommand, Result<CustomerDetailDto>>
{
    public async Task<Result<CustomerDetailDto>> Handle(
        UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            return Result.Failure<CustomerDetailDto>(Error.NotFound("customer.notFound", "Customer not found."));

        // Set the original row version so EF can detect a concurrent change.
        db.Customers.Entry(customer).Property(c => c.RowVersion).OriginalValue =
            Convert.FromBase64String(request.RowVersion);

        customer.Name = request.Name.Trim();
        customer.Email = request.Email?.Trim();
        customer.PhoneNumber = request.PhoneNumber?.Trim();
        customer.CountryCode = request.CountryCode?.ToUpperInvariant();
        customer.Status = request.Status;
        customer.Touch(clock.UtcNow);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<CustomerDetailDto>(
                Error.Conflict("customer.concurrencyConflict",
                    "The customer was modified by someone else. Reload and try again."));
        }

        return Result.Success(customer.ToDetail());
    }
}
