using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Features.Compliance;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance")]
[Authorize]
public sealed class ComplianceController(ISender sender, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>The compliance review queue. Defaults to open holds; pass ?status= for history.</summary>
    [HttpGet("cases")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] ComplianceCaseStatus? status = ComplianceCaseStatus.Open,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new GetComplianceQueueQuery(status), cancellationToken)).ToActionResult(this);

    /// <summary>All compliance cases raised against a specific payment.</summary>
    [HttpGet("/api/v{version:apiVersion}/payments/{paymentId:guid}/compliance")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetForPayment(Guid paymentId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPaymentComplianceCasesQuery(paymentId), cancellationToken))
            .ToActionResult(this);

    [HttpPost("cases/{caseId:guid}/clear")]
    [Authorize(Policy = AuthorizationPolicies.CanManageCompliance)]
    public async Task<IActionResult> Clear(
        Guid caseId, ReviewComplianceCaseRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new ReviewComplianceCaseCommand(caseId, ComplianceReviewAction.Clear,
                currentUser.UserId?.ToString(), currentUser.Email, request.Notes),
            cancellationToken)).ToActionResult(this);

    [HttpPost("cases/{caseId:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanManageCompliance)]
    public async Task<IActionResult> Reject(
        Guid caseId, ReviewComplianceCaseRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new ReviewComplianceCaseCommand(caseId, ComplianceReviewAction.Reject,
                currentUser.UserId?.ToString(), currentUser.Email, request.Notes),
            cancellationToken)).ToActionResult(this);
}
