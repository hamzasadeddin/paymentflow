using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Features.Audit;

namespace PaymentFlow.Api.Controllers;

/// <summary>
/// Read-only viewer over the security audit trail. Gated by <c>CanReadAuditLog</c>
/// (admins, compliance, and the read-only auditor) — deliberately narrower than
/// general operations read.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-events")]
[Authorize]
public sealed class AuditController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadAuditLog)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? eventType = null, [FromQuery] bool? succeeded = null,
        [FromQuery] string? userQuery = null,
        [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditEventsQuery(page, pageSize, eventType, succeeded, userQuery, fromUtc, toUtc, search);
        return (await sender.Send(query, cancellationToken)).ToActionResult(this);
    }

    [HttpGet("event-types")]
    [Authorize(Policy = AuthorizationPolicies.CanReadAuditLog)]
    public async Task<IActionResult> GetEventTypes(CancellationToken cancellationToken)
        => (await sender.Send(new GetAuditEventTypesQuery(), cancellationToken)).ToActionResult(this);
}
