using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Customers;

public record CreateCustomerCommand(
    CustomerType Type, string Name, string? Email, string? PhoneNumber, string? CountryCode)
    : IRequest<Result<CustomerDetailDto>>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneNumber).MaximumLength(32);
        RuleFor(x => x.CountryCode).Length(2).When(x => !string.IsNullOrWhiteSpace(x.CountryCode));
    }
}

public sealed class CreateCustomerCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CreateCustomerCommand, Result<CustomerDetailDto>>
{
    public async Task<Result<CustomerDetailDto>> Handle(
        CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Type = request.Type,
            Name = request.Name.Trim(),
            Email = request.Email?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            CountryCode = request.CountryCode?.ToUpperInvariant(),
            CustomerReference = await GenerateReferenceAsync(cancellationToken),
            CreatedAtUtc = clock.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(customer.ToDetail());
    }

    private async Task<string> GenerateReferenceAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var countThisYear = await db.Customers
            .CountAsync(c => c.CreatedAtUtc.Year == year, cancellationToken);
        return $"CUST-{year}-{(countThisYear + 1):D6}";
    }
}
