using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Features.Accounts;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounts")]
[Authorize]
public sealed class AccountsController(ISender sender, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("{accountId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetAccount(Guid accountId, CancellationToken cancellationToken)
        => (await sender.Send(new GetAccountByIdQuery(accountId), cancellationToken)).ToActionResult(this);

    [HttpGet("{accountId:guid}/reveal-number")]
    [Authorize(Policy = AuthorizationPolicies.CanRevealAccountNumbers)]
    public async Task<IActionResult> RevealNumber(Guid accountId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RevealAccountNumberQuery(accountId, currentUser.UserId, ClientIp()), cancellationToken);
        return result.IsSuccess
            ? Ok(new { accountId, accountNumber = result.Value })
            : result.ToActionResult(this);
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
