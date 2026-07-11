using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentFlow.Api.Extensions;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Application.Features.Accounts;
using PaymentFlow.Application.Features.Customers;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
[Authorize]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] CustomerStatus? status = null, [FromQuery] CustomerType? type = null,
        CancellationToken cancellationToken = default)
    {
        var paging = new PagedRequest { Page = page, PageSize = pageSize, Search = search, SortBy = sortBy, SortDescending = sortDescending };
        var result = await sender.Send(new GetCustomersQuery(paging, status, type), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{customerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetCustomer(Guid customerId, CancellationToken cancellationToken)
        => (await sender.Send(new GetCustomerByIdQuery(customerId), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageCustomers)]
    public async Task<IActionResult> CreateCustomer(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCustomerCommand(request.Type, request.Name, request.Email, request.PhoneNumber, request.CountryCode),
            cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetCustomer), new { customerId = result.Value.Id, version = "1.0" }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPut("{customerId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageCustomers)]
    public async Task<IActionResult> UpdateCustomer(
        Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateCustomerCommand(customerId, request.Name, request.Email, request.PhoneNumber,
                request.CountryCode, request.Status, request.RowVersion),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{customerId:guid}/accounts")]
    [Authorize(Policy = AuthorizationPolicies.CanReadOperations)]
    public async Task<IActionResult> GetCustomerAccounts(Guid customerId, CancellationToken cancellationToken)
        => (await sender.Send(new GetCustomerAccountsQuery(customerId), cancellationToken)).ToActionResult(this);

    [HttpPost("{customerId:guid}/accounts")]
    [Authorize(Policy = AuthorizationPolicies.CanManageCustomers)]
    public async Task<IActionResult> CreateAccount(
        Guid customerId, CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateAccountCommand(customerId, request.Currency, request.OpeningBalance, request.DailyLimit),
            cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(AccountsController.GetAccount), "Accounts",
                new { accountId = result.Value.Id, version = "1.0" }, result.Value)
            : result.ToActionResult(this);
    }

    public record CreateAccountRequest(string Currency, decimal OpeningBalance, decimal DailyLimit);
}
