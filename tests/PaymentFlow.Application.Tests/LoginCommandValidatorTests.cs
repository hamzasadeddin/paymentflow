using PaymentFlow.Application.Features.Auth;
using Xunit;

namespace PaymentFlow.Application.Tests;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(new LoginCommand("admin@paymentflow.local", "Secret123!", null));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Secret123!")]
    [InlineData("not-an-email", "Secret123!")]
    [InlineData("admin@paymentflow.local", "")]
    public void Invalid_command_fails(string email, string password)
    {
        var result = _validator.Validate(new LoginCommand(email, password, null));
        Assert.False(result.IsValid);
    }
}
