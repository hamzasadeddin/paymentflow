using PaymentFlow.Application.Features.Accounts;
using Xunit;

namespace PaymentFlow.Application.Tests;

public class AccountValidatorTests
{
    private readonly CreateAccountCommandValidator _validator = new();

    [Fact]
    public void Supported_currency_passes()
        => Assert.True(_validator.Validate(new CreateAccountCommand(Guid.NewGuid(), "USD", 100m, 500m)).IsValid);

    [Fact]
    public void Unsupported_currency_fails()
        => Assert.False(_validator.Validate(new CreateAccountCommand(Guid.NewGuid(), "XYZ", 100m, 500m)).IsValid);

    [Fact]
    public void Negative_balance_fails()
        => Assert.False(_validator.Validate(new CreateAccountCommand(Guid.NewGuid(), "USD", -1m, 500m)).IsValid);
}
