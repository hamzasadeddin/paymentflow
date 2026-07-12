using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Features.Approvals;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/approvals")]
[Authorize]
public sealed class ApprovalsController(ISender sender) : ControllerBase
{
    /// <summary>The unified maker-checker queue: payments and beneficiaries awaiting approval.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
        => (await sender.Send(new GetApprovalQueueQuery(), cancellationToken)).ToActionResult(this);
}
