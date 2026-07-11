namespace PaymentFlow.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
