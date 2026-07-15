using PaymentFlow.Application.Features.Admin;
using PaymentFlow.Domain.Constants;
using Xunit;

namespace PaymentFlow.Application.Tests;

public class RuleValidatorTests
{
    [Fact]
    public void Approval_dual_below_auto_fails()
    {
        var validator = new UpdateApprovalRulesCommandValidator();
        var result = validator.Validate(new UpdateApprovalRulesCommand(5000m, 1000m, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Approval_ordered_thresholds_pass()
    {
        var validator = new UpdateApprovalRulesCommandValidator();
        var result = validator.Validate(new UpdateApprovalRulesCommand(1000m, 5000m, "abc"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("USA")]
    [InlineData("U")]
    public void Screening_bad_country_code_fails(string code)
    {
        var validator = new UpdateScreeningRulesCommandValidator();
        var result = validator.Validate(new UpdateScreeningRulesCommand(
            true, [], [code], 5000m, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Screening_valid_passes()
    {
        var validator = new UpdateScreeningRulesCommandValidator();
        var result = validator.Validate(new UpdateScreeningRulesCommand(
            true, ["Gulf Freight"], ["IR", "KP"], 5000m, null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Processing_latency_min_above_max_fails()
    {
        var validator = new UpdateProcessingRulesCommandValidator();
        var result = validator.Validate(new UpdateProcessingRulesCommand(
            true, 5, 10, 2000, 500, 13, null));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(-1)]
    public void Processing_fail_on_cents_out_of_range_fails(int cents)
    {
        var validator = new UpdateProcessingRulesCommandValidator();
        var result = validator.Validate(new UpdateProcessingRulesCommand(
            true, 5, 10, 0, 2500, cents, null));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("44")]
    [InlineData("x")]
    public void Reconciliation_bad_drop_digit_fails(string digit)
    {
        var validator = new UpdateReconciliationRulesCommandValidator();
        var result = validator.Validate(new UpdateReconciliationRulesCommand(
            true, digit, 999m, 50, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateUser_unknown_role_fails()
    {
        var validator = new CreateUserCommandValidator();
        var result = validator.Validate(new CreateUserCommand(
            "new@paymentflow.local", "New User", "Demo!Passw0rd1", ["Wizard"]));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateUser_valid_passes()
    {
        var validator = new CreateUserCommandValidator();
        var result = validator.Validate(new CreateUserCommand(
            "new@paymentflow.local", "New User", "Demo!Passw0rd1", [Roles.OperationsAnalyst]));
        Assert.True(result.IsValid);
    }
}
