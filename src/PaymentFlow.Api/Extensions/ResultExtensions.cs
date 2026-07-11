using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<TValue>(this Result<TValue> result, ControllerBase controller)
        => result.IsSuccess ? controller.Ok(result.Value) : ToProblem(result.Error!, controller);

    public static IActionResult ToNoContentResult(this Result result, ControllerBase controller)
        => result.IsSuccess ? controller.NoContent() : ToProblem(result.Error!, controller);

    private static IActionResult ToProblem(Error error, ControllerBase controller)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return controller.Problem(
            statusCode: statusCode,
            title: error.Message,
            type: $"https://paymentflow.local/errors/{error.Code}");
    }
}
