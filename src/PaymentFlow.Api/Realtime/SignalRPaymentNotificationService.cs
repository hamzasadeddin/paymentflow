using Microsoft.AspNetCore.SignalR;
using PaymentFlow.Api.Hubs;
using PaymentFlow.Application.Abstractions;

namespace PaymentFlow.Api.Realtime;

/// <summary>
/// Pushes payment status changes to all connected clients over
/// <see cref="PaymentsHub"/>. This is the API-layer implementation of the
/// Application's <see cref="IPaymentNotificationService"/> seam, keeping the
/// settlement logic free of any SignalR dependency.
/// </summary>
public sealed class SignalRPaymentNotificationService(IHubContext<PaymentsHub> hub)
    : IPaymentNotificationService
{
    public Task PaymentStatusChangedAsync(
        PaymentStatusChangedNotification notification, CancellationToken cancellationToken = default)
        => hub.Clients.All.SendAsync(PaymentsHub.StatusChangedEvent, notification, cancellationToken);
}
