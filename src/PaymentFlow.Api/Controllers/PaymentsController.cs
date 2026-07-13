using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Application.Features.Approvals;
using PaymentFlow.Application.Features.Payments;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Authorize]
public sealed class PaymentsController(ISender sender, ICurrentUserService currentUser) : ControllerBase
{
    private const string IdempotencyHeader = "Idempotency-Key";

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] Guid? sourceAccountId = null, [FromQuery] Guid? beneficiaryId = null,
        [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var paging = new PagedRequest
        {
            Page = page, PageSize = pageSize, Search = search,
            SortBy = sortBy, SortDescending = sortDescending
        };
        var result = await sender.Send(
            new GetPaymentsQuery(paging, status, sourceAccountId, beneficiaryId, fromUtc, toUtc),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{paymentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetPayment(Guid paymentId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPaymentByIdQuery(paymentId), cancellationToken)).ToActionResult(this);

    /// <summary>Payment counts by status, for the dashboard overview.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        => (await sender.Send(new GetPaymentStatusSummaryQuery(), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManagePayments)]
    public async Task<IActionResult> CreatePayment(
        CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers.TryGetValue(IdempotencyHeader, out var value)
            ? value.ToString()
            : null;

        var result = await sender.Send(
            new CreatePaymentCommand(request.SourceAccountId, request.BeneficiaryId,
                request.Amount, request.Currency, request.Description, idempotencyKey,
                currentUser.UserId?.ToString()),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPayment), new { paymentId = result.Value.Id, version = "1.0" }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPost("{paymentId:guid}/submit-for-approval")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePayments)]
    public async Task<IActionResult> SubmitForApproval(Guid paymentId, CancellationToken cancellationToken)
        => (await sender.Send(new SubmitPaymentCommand(paymentId), cancellationToken)).ToActionResult(this);

    [HttpPost("{paymentId:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePayments)]
    public async Task<IActionResult> Cancel(Guid paymentId, CancellationToken cancellationToken)
        => (await sender.Send(new CancelPaymentCommand(paymentId), cancellationToken)).ToActionResult(this);

    [HttpPost("{paymentId:guid}/process")]
    [Authorize(Policy = AuthorizationPolicies.CanManagePayments)]
    public async Task<IActionResult> Process(Guid paymentId, CancellationToken cancellationToken)
        => (await sender.Send(new ProcessPaymentCommand(paymentId), cancellationToken)).ToActionResult(this);

    [HttpPost("{paymentId:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanApprovePayments)]
    public async Task<IActionResult> Approve(
        Guid paymentId, ReviewPaymentRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new TransitionPaymentCommand(paymentId, PaymentReviewAction.Approve,
                currentUser.UserId?.ToString(), currentUser.Email, request.Notes),
            cancellationToken)).ToActionResult(this);

    [HttpPost("{paymentId:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanApprovePayments)]
    public async Task<IActionResult> Reject(
        Guid paymentId, ReviewPaymentRequest request, CancellationToken cancellationToken)
        => (await sender.Send(
            new TransitionPaymentCommand(paymentId, PaymentReviewAction.Reject,
                currentUser.UserId?.ToString(), currentUser.Email, request.Notes),
            cancellationToken)).ToActionResult(this);

    [HttpGet("{paymentId:guid}/approvals")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetApprovals(Guid paymentId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPaymentApprovalsQuery(paymentId), cancellationToken)).ToActionResult(this);
}
