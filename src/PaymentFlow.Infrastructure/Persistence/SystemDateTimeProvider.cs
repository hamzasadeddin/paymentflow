using PaymentFlow.Application.Abstractions;

namespace PaymentFlow.Infrastructure.Persistence;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
