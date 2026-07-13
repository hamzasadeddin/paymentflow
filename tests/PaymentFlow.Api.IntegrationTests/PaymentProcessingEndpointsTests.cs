using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class PaymentProcessingEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Creates a customer, an active account, and an approved beneficiary.</summary>
    private async Task<(Guid AccountId, Guid BeneficiaryId)> SetupAsync(
        HttpClient analyst, HttpClient approver,
        decimal openingBalance = 1000m, decimal dailyLimit = 100000m)
    {
        var cust = await analyst.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Proc Test", countryCode = "US" });
        var customer = await cust.Content.ReadFromJsonAsync<IdHolder>(Json);

        var acc = await analyst.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency = "USD", openingBalance, dailyLimit });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);

        var ben = await analyst.PostAsJsonAsync("/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Payee Co", accountNumber = "9990001112223", currency = "USD" });
        var beneficiary = await ben.Content.ReadFromJsonAsync<IdHolder>(Json);

        await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        return (account!.Id, beneficiary.Id);
    }

    /// <summary>Drives a fresh payment all the way to Approved and returns it.</summary>
    private async Task<PaymentDto> ApprovedPaymentAsync(
        HttpClient analyst, HttpClient approver, Guid accountId, Guid beneficiaryId, decimal amount)
    {
        var create = await analyst.PostAsJsonAsync("/api/v1/payments",
            new { sourceAccountId = accountId, beneficiaryId, amount, currency = "USD" });
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        var approve = await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "ok" });
        var approved = await approve.Content.ReadFromJsonAsync<PaymentDto>(Json);

        Assert.Equal(3, approved!.Status); // Approved
        return approved;
    }

    private async Task<AccountBalance> BalanceAsync(HttpClient client, Guid accountId)
    {
        var response = await client.GetAsync($"/api/v1/accounts/{accountId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountBalance>(Json))!;
    }

    [Fact]
    public async Task Process_settles_an_approved_payment_and_debits_the_ledger()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver, openingBalance: 1000m);

        var payment = await ApprovedPaymentAsync(analyst, approver, accountId, beneficiaryId, 100m);

        var process = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.OK, process.StatusCode);
        var settled = await process.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(5, settled!.Status); // Completed

        // Reserve on approve took Available to 900; Settle on complete takes Ledger to 900.
        var balance = await BalanceAsync(analyst, accountId);
        Assert.Equal(900m, balance.AvailableBalance);
        Assert.Equal(900m, balance.LedgerBalance);
    }

    [Fact]
    public async Task Process_fails_deterministically_for_the_sentinel_amount_and_releases_the_reservation()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver, openingBalance: 1000m);

        // Cents == 13 => deterministic failure (FailOnCents pinned to 13 in the test host).
        var payment = await ApprovedPaymentAsync(analyst, approver, accountId, beneficiaryId, 100.13m);

        var process = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.OK, process.StatusCode);
        var failed = await process.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(6, failed!.Status); // Failed
        Assert.False(string.IsNullOrWhiteSpace(failed.FailureReason));

        // The reservation is returned, so both balances are back to the opening figure.
        var balance = await BalanceAsync(analyst, accountId);
        Assert.Equal(1000m, balance.AvailableBalance);
        Assert.Equal(1000m, balance.LedgerBalance);
    }

    [Fact]
    public async Task Process_on_a_non_approved_payment_returns_conflict()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        // A freshly created (Draft) payment cannot be processed.
        var create = await analyst.PostAsJsonAsync("/api/v1/payments",
            new { sourceAccountId = accountId, beneficiaryId, amount = 100m, currency = "USD" });
        var draft = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        var process = await analyst.PostAsync($"/api/v1/payments/{draft!.Id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, process.StatusCode);
    }

    [Fact]
    public async Task Process_is_exactly_once_second_attempt_conflicts_and_does_not_double_settle()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver, openingBalance: 1000m);

        var payment = await ApprovedPaymentAsync(analyst, approver, accountId, beneficiaryId, 100m);

        var first = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Balance reflects exactly one settlement.
        var balance = await BalanceAsync(analyst, accountId);
        Assert.Equal(900m, balance.AvailableBalance);
        Assert.Equal(900m, balance.LedgerBalance);
    }

    [Fact]
    public async Task Auditor_cannot_process_a_payment()
    {
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var process = await auditor.PostAsync($"/api/v1/payments/{Guid.NewGuid()}/process", null);
        Assert.Equal(HttpStatusCode.Forbidden, process.StatusCode);
    }

    [Fact]
    public async Task Process_requires_authentication()
    {
        var anonymous = factory.CreateClient();
        var process = await anonymous.PostAsync($"/api/v1/payments/{Guid.NewGuid()}/process", null);
        Assert.Equal(HttpStatusCode.Unauthorized, process.StatusCode);
    }

    private sealed record IdHolder(Guid Id);
    private sealed record PaymentDto(Guid Id, int Status, decimal Amount, string PaymentReference, string? FailureReason);
    private sealed record AccountBalance(decimal AvailableBalance, decimal LedgerBalance);
}

/// <summary>
/// A factory variant with the background worker enabled, used to prove that
/// Approved payments settle automatically without a manual trigger.
/// </summary>
public sealed class AutoProcessingApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Applied after the base configuration, so these win: turn the worker on
        // with a fast poll and no latency for a quick, deterministic test.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:AutoProcessEnabled"] = "true",
                ["Processing:PollingIntervalSeconds"] = "1",
                ["Processing:SimulatedLatencyMsMin"] = "0",
                ["Processing:SimulatedLatencyMsMax"] = "0",
                ["Processing:FailOnCents"] = "13"
            }));
    }
}

public class PaymentWorkerTests(AutoProcessingApiFactory factory) : IClassFixture<AutoProcessingApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Background_worker_settles_an_approved_payment_without_a_manual_trigger()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);

        var cust = await analyst.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Worker Test", countryCode = "US" });
        var customer = await cust.Content.ReadFromJsonAsync<IdHolder>(Json);
        var acc = await analyst.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency = "USD", openingBalance = 1000m, dailyLimit = 100000m });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);
        var ben = await analyst.PostAsJsonAsync("/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Payee", accountNumber = "9990001112223", currency = "USD" });
        var beneficiary = await ben.Content.ReadFromJsonAsync<IdHolder>(Json);
        await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        var create = await analyst.PostAsJsonAsync("/api/v1/payments",
            new { sourceAccountId = account!.Id, beneficiaryId = beneficiary.Id, amount = 100m, currency = "USD" });
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "ok" });

        // No manual /process call: the worker should pick it up within a few ticks.
        var status = await PollForStatusAsync(analyst, payment.Id, target: 5, timeout: TimeSpan.FromSeconds(15));
        Assert.Equal(5, status); // Completed
    }

    private static async Task<int> PollForStatusAsync(
        HttpClient client, Guid paymentId, int target, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var last = 0;
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/v1/payments/{paymentId}");
            var payment = await response.Content.ReadFromJsonAsync<PaymentDto>(Json);
            last = payment!.Status;
            if (last == target)
                return last;
            await Task.Delay(250);
        }

        return last;
    }

    private sealed record IdHolder(Guid Id);
    private sealed record PaymentDto(Guid Id, int Status);
}
