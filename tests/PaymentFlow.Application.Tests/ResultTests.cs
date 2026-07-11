using PaymentFlow.Application.Common;
using Xunit;

namespace PaymentFlow.Application.Tests;

public class ResultTests
{
    [Fact]
    public void Success_result_exposes_value()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_result_throws_on_value_access()
    {
        var result = Result.Failure<int>(Error.NotFound("x.notFound", "Not found"));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
