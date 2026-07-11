using PaymentFlow.Application.Features.Payments;
using Xunit;

namespace PaymentFlow.Application.Tests;

public class PaymentValidatorTests
{
    private readonly CreatePaymentCommandValidator _validator = new();

    private static CreatePaymentCommand Command(decimal amount = 100m, string currency = "USD") =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount, currency, "Invoice", null);

    [Fact]
    public void Valid_command_passes()
        => Assert.True(_validator.Validate(Command()).IsValid);

    [Fact]
    public void Zero_amount_fails()
        => Assert.False(_validator.Validate(Command(amount: 0m)).IsValid);

    [Fact]
    public void Negative_amount_fails()
        => Assert.False(_validator.Validate(Command(amount: -5m)).IsValid);

    [Fact]
    public void Unsupported_currency_fails()
        => Assert.False(_validator.Validate(Command(currency: "XYZ")).IsValid);

    [Fact]
    public void Empty_source_account_fails()
        => Assert.False(_validator.Validate(
            new CreatePaymentCommand(Guid.Empty, Guid.NewGuid(), 100m, "USD", null, null)).IsValid);
}
