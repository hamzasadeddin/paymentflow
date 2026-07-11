using PaymentFlow.Application.Abstractions;

namespace PaymentFlow.Application.Tests;

public sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}
