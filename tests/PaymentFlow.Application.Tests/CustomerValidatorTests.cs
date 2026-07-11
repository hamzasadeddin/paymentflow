using PaymentFlow.Application.Features.Customers;
using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Application.Tests;

public class CustomerValidatorTests
{
    private readonly CreateCustomerCommandValidator _validator = new();

    [Fact]
    public void Valid_individual_passes()
    {
        var result = _validator.Validate(
            new CreateCustomerCommand(CustomerType.Individual, "Layla Haddad", "layla@example.com", null, "JO"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_fails(string name)
    {
        var result = _validator.Validate(new CreateCustomerCommand(CustomerType.Individual, name, null, null, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Bad_country_code_fails()
    {
        var result = _validator.Validate(
            new CreateCustomerCommand(CustomerType.Business, "Cedar Trading", null, null, "JOR"));
        Assert.False(result.IsValid);
    }
}
