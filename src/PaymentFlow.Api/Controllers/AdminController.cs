using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Application.Features.Admin;

namespace PaymentFlow.Api.Controllers;

/// <summary>
/// Administration: user &amp; role management and rules configuration. Every action
/// is gated by <c>CanAdminister</c> (administrator only); the acting user is taken
/// from the authenticated principal inside the command handlers, and every mutation
/// is audited.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = AuthorizationPolicies.CanAdminister)]
public sealed class AdminController(ISender sender) : ControllerBase
{
    // ---------- Users ----------

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var paging = new PagedRequest { Page = page, PageSize = pageSize, Search = search };
        return (await sender.Send(new GetUsersQuery(paging), cancellationToken)).ToActionResult(this);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new CreateUserCommand(request.Email, request.DisplayName, request.Password, request.Roles),
            cancellationToken)).ToActionResult(this);

    [HttpPost("users/{userId:guid}/activate")]
    public async Task<IActionResult> ActivateUser(Guid userId, CancellationToken cancellationToken)
        => (await sender.Send(new SetUserActiveCommand(userId, true), cancellationToken)).ToActionResult(this);

    [HttpPost("users/{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid userId, CancellationToken cancellationToken)
        => (await sender.Send(new SetUserActiveCommand(userId, false), cancellationToken)).ToActionResult(this);

    [HttpPut("users/{userId:guid}/roles")]
    public async Task<IActionResult> SetUserRoles(
        Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new SetUserRolesCommand(userId, request.Roles), cancellationToken))
            .ToActionResult(this);

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(
        Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ResetUserPasswordCommand(userId, request.NewPassword), cancellationToken))
            .ToNoContentResult(this);

    // ---------- Rules ----------

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken cancellationToken)
        => (await sender.Send(new GetRulesQuery(), cancellationToken)).ToActionResult(this);

    [HttpPut("rules/approval")]
    public async Task<IActionResult> UpdateApprovalRules(
        UpdateApprovalRulesRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new UpdateApprovalRulesCommand(request.AutoApproveBelow, request.DualApprovalAtOrAbove, request.RowVersion),
            cancellationToken)).ToActionResult(this);

    [HttpPut("rules/screening")]
    public async Task<IActionResult> UpdateScreeningRules(
        UpdateScreeningRulesRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new UpdateScreeningRulesCommand(request.AutoScreenOnSubmit, request.WatchlistBeneficiaryNames,
                request.WatchlistCountryCodes, request.SinglePaymentReviewLimit, request.RowVersion),
            cancellationToken)).ToActionResult(this);

    [HttpPut("rules/reconciliation")]
    public async Task<IActionResult> UpdateReconciliationRules(
        UpdateReconciliationRulesRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new UpdateReconciliationRulesCommand(request.IntroduceSyntheticBreaks, request.DropReferenceEndingIn,
                request.PhantomAmount, request.AmountDriftMinorUnits, request.RowVersion),
            cancellationToken)).ToActionResult(this);

    [HttpPut("rules/processing")]
    public async Task<IActionResult> UpdateProcessingRules(
        UpdateProcessingRulesRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new UpdateProcessingRulesCommand(request.AutoProcessEnabled, request.PollingIntervalSeconds,
                request.BatchSize, request.SimulatedLatencyMsMin, request.SimulatedLatencyMsMax,
                request.FailOnCents, request.RowVersion),
            cancellationToken)).ToActionResult(this);
}
