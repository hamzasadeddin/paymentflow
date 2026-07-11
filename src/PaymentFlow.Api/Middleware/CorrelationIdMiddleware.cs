using Serilog.Context;

namespace PaymentFlow.Api.Middleware;

/// <summary>
/// Accepts an inbound X-Correlation-Id or generates one, echoes it on the
/// response, and pushes it into the Serilog context so every log line for the
/// request can be traced end to end (including from the Angular interceptor).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.ToString()
                : Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
