using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Abstractions;

/// <summary>A payment status transition worth surfacing to connected clients.</summary>
public sealed record PaymentStatusChangedNotification(
    Guid PaymentId,
    string PaymentReference,
    PaymentStatus Status,
    string? FailureReason,
    DateTime UpdatedAtUtc);

/// <summary>
/// Transport-agnostic seam for pushing payment status changes to clients. The
/// Application layer depends only on this; the SignalR implementation lives in
/// the API layer, so settlement logic stays free of any delivery concern and is
/// trivially testable with a fake notifier.
/// </summary>
public interface IPaymentNotificationService
{
    Task PaymentStatusChangedAsync(
        PaymentStatusChangedNotification notification, CancellationToken cancellationToken = default);
}
