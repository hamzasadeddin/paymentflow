using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Application.Features.Beneficiaries;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/beneficiaries")]
[Authorize]
public sealed class BeneficiariesController(ISender sender, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetBeneficiaries(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] Guid? customerId = null, [FromQuery] BeneficiaryStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var paging = new PagedRequest { Page = page, PageSize = pageSize, Search = search, SortBy = sortBy, SortDescending = sortDescending };
        var result = await sender.Send(new GetBeneficiariesQuery(paging, customerId, status), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{beneficiaryId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetBeneficiary(Guid beneficiaryId, CancellationToken cancellationToken)
        => (await sender.Send(new GetBeneficiaryByIdQuery(beneficiaryId), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageBeneficiaries)]
    public async Task<IActionResult> CreateBeneficiary(CreateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateBeneficiaryCommand(request.CustomerId, request.Name, request.AccountNumber,
                request.BankName, request.BankIdentifierCode, request.Currency, request.CountryCode),
            cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetBeneficiary), new { beneficiaryId = result.Value.Id, version = "1.0" }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPut("{beneficiaryId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageBeneficiaries)]
    public async Task<IActionResult> UpdateBeneficiary(
        Guid beneficiaryId, UpdateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateBeneficiaryCommand(beneficiaryId, request.Name, request.AccountNumber, request.BankName,
                request.BankIdentifierCode, request.Currency, request.CountryCode, request.RowVersion),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{beneficiaryId:guid}/submit-for-approval")]
    [Authorize(Policy = AuthorizationPolicies.CanManageBeneficiaries)]
    public async Task<IActionResult> SubmitForApproval(Guid beneficiaryId, CancellationToken cancellationToken)
        => (await sender.Send(
            new TransitionBeneficiaryCommand(beneficiaryId, BeneficiaryTransition.Submit, currentUser.UserId?.ToString(), null),
            cancellationToken)).ToActionResult(this);

    [HttpPost("{beneficiaryId:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveBeneficiaries)]
    public async Task<IActionResult> Approve(
        Guid beneficiaryId, ReviewBeneficiaryRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new TransitionBeneficiaryCommand(beneficiaryId, BeneficiaryTransition.Approve, currentUser.UserId?.ToString(), request.Notes),
            cancellationToken)).ToActionResult(this);

    [HttpPost("{beneficiaryId:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveBeneficiaries)]
    public async Task<IActionResult> Reject(
        Guid beneficiaryId, ReviewBeneficiaryRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new TransitionBeneficiaryCommand(beneficiaryId, BeneficiaryTransition.Reject, currentUser.UserId?.ToString(), request.Notes),
            cancellationToken)).ToActionResult(this);
}
