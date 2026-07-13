using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PaymentFlow.Api.Hubs;

/// <summary>
/// Real-time channel for payment status changes. The hub is server-to-client
/// only: it exposes no client-invokable methods, so authentication is the entire
/// security surface. Clients authenticate with the same JWT used for the API,
/// passed as an <c>access_token</c> query-string parameter on the handshake.
/// </summary>
[Authorize]
public sealed class PaymentsHub : Hub
{
    /// <summary>The client-side handler name for status-change broadcasts.</summary>
    public const string StatusChangedEvent = "paymentStatusChanged";
}
