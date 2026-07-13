using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Features.Reconciliation;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reconciliation")]
[Authorize]
public sealed class ReconciliationController(ISender sender, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Runs a reconciliation pass and returns its summary.</summary>
    [HttpPost("run")]
    [Authorize(Policy = AuthorizationPolicies.CanReconcile)]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
        => (await sender.Send(new RunReconciliationCommand(currentUser.UserId?.ToString()), cancellationToken))
            .ToActionResult(this);

    [HttpGet("runs")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetRuns(CancellationToken cancellationToken)
        => (await sender.Send(new GetReconciliationRunsQuery(), cancellationToken)).ToActionResult(this);

    [HttpGet("runs/{runId:guid}/breaks")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetBreaks(
        Guid runId, [FromQuery] BreakStatus? status = null, CancellationToken cancellationToken = default)
        => (await sender.Send(new GetRunBreaksQuery(runId, status), cancellationToken)).ToActionResult(this);

    [HttpPost("breaks/{breakId:guid}/resolve")]
    [Authorize(Policy = AuthorizationPolicies.CanReconcile)]
    public async Task<IActionResult> Resolve(
        Guid breakId, ResolveBreakRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new ReviewBreakCommand(breakId, BreakReviewAction.Resolve, currentUser.UserId?.ToString(), request.Notes),
            cancellationToken)).ToActionResult(this);

    [HttpPost("breaks/{breakId:guid}/ignore")]
    [Authorize(Policy = AuthorizationPolicies.CanReconcile)]
    public async Task<IActionResult> Ignore(
        Guid breakId, ResolveBreakRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new ReviewBreakCommand(breakId, BreakReviewAction.Ignore, currentUser.UserId?.ToString(), request.Notes),
            cancellationToken)).ToActionResult(this);
}
