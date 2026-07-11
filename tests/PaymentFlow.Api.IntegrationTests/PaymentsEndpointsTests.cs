using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class PaymentsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Creates a customer, an active account, and an approved beneficiary.</summary>
    private async Task<(Guid AccountId, Guid BeneficiaryId)> SetupAsync(
        HttpClient analyst, HttpClient approver, string currency = "USD",
        decimal openingBalance = 1000m, decimal dailyLimit = 1000m)
    {
        var cust = await analyst.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Pay Test", countryCode = "US" });
        var customer = await cust.Content.ReadFromJsonAsync<IdHolder>(Json);

        var acc = await analyst.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency, openingBalance, dailyLimit });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);

        var ben = await analyst.PostAsJsonAsync("/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Payee Co", accountNumber = "9990001112223", currency });
        var beneficiary = await ben.Content.ReadFromJsonAsync<IdHolder>(Json);

        await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        return (account!.Id, beneficiary.Id);
    }

    private static HttpRequestMessage CreatePayment(
        Guid accountId, Guid beneficiaryId, decimal amount, string? idempotencyKey, string currency = "USD")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                sourceAccountId = accountId, beneficiaryId, amount, currency
            })
        };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    [Fact]
    public async Task Create_with_same_idempotency_key_returns_same_payment()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var key = Guid.NewGuid().ToString();

        var first = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstPayment = await first.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.StartsWith("*", firstPayment!.SourceAccountMaskedNumber);

        var second = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, key));
        var secondPayment = await second.Content.ReadFromJsonAsync<PaymentDto>(Json);

        Assert.Equal(firstPayment.Id, secondPayment!.Id);
        Assert.Equal(firstPayment.PaymentReference, secondPayment.PaymentReference);
    }

    [Fact]
    public async Task Submit_moves_payment_to_pending_approval()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(1, payment!.Status); // Draft

        var submit = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/submit-for-approval", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitted = await submit.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(2, submitted!.Status); // PendingApproval
    }

    [Fact]
    public async Task Cancel_moves_draft_to_cancelled()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        var cancel = await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(7, cancelled!.Status); // Cancelled
    }

    [Fact]
    public async Task Approve_reserves_funds_from_available_balance()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var before = await analyst.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{accountId}", Json);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);

        var approve = await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "ok" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(3, approved!.Status); // Approved

        var after = await analyst.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{accountId}", Json);
        Assert.Equal(before!.AvailableBalance - 100m, after!.AvailableBalance);
        Assert.Equal(before.LedgerBalance, after.LedgerBalance); // ledger untouched until settlement
    }

    [Fact]
    public async Task Analyst_cannot_approve_a_payment()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);

        var approve = await analyst.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task Approving_a_draft_payment_conflicts()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 100m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        // Skip submit -> approve a Draft -> invalid transition -> 409.
        var approve = await approver.PostAsJsonAsync($"/api/v1/payments/{payment!.Id}/approve", new { notes = "x" });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Fact]
    public async Task Payment_exceeding_available_funds_conflicts_on_submit()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        // Balance 1000, but a generous daily limit so funds (not the limit) is the blocker.
        var (accountId, beneficiaryId) = await SetupAsync(
            analyst, approver, openingBalance: 1000m, dailyLimit: 100000m);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 5000m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        var submit = await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode); // insufficient funds
    }

    [Fact]
    public async Task Payment_exceeding_daily_limit_conflicts_on_submit()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        // Ample balance, but a tight daily limit: the limit is the blocker.
        var (accountId, beneficiaryId) = await SetupAsync(
            analyst, approver, openingBalance: 100000m, dailyLimit: 500m);

        var create = await analyst.SendAsync(CreatePayment(accountId, beneficiaryId, 1000m, null));
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        var submit = await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode); // daily limit exceeded
    }

    [Fact]
    public async Task Auditor_cannot_create_a_payment()
    {
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var create = await auditor.SendAsync(CreatePayment(Guid.NewGuid(), Guid.NewGuid(), 10m, null));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_list_payments()
    {
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var list = await auditor.GetAsync("/api/v1/payments?pageSize=5");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    private sealed record IdHolder(Guid Id);
    private sealed record PaymentDto(Guid Id, int Status, decimal Amount, string PaymentReference, string SourceAccountMaskedNumber);
    private sealed record AccountDto(decimal AvailableBalance, decimal LedgerBalance);
}
