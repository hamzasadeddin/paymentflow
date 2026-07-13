using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class ComplianceEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Customer + active account + an approved beneficiary whose name is watchlisted.</summary>
    private async Task<(Guid AccountId, Guid BeneficiaryId)> SetupWatchlistedAsync(
        HttpClient analyst, HttpClient approver, decimal opening = 1000m)
    {
        var cust = await analyst.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Compliance Test", countryCode = "US" });
        var customer = await cust.Content.ReadFromJsonAsync<IdHolder>(Json);

        var acc = await analyst.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency = "USD", openingBalance = opening, dailyLimit = 100000m });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);

        // "Gulf Freight Services" contains the watchlist term "Gulf Freight".
        var ben = await analyst.PostAsJsonAsync("/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Gulf Freight Services", accountNumber = "9990001112223", currency = "USD" });
        var beneficiary = await ben.Content.ReadFromJsonAsync<IdHolder>(Json);

        await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        return (account!.Id, beneficiary.Id);
    }

    /// <summary>Creates and submits a payment (which triggers screening); returns it.</summary>
    private async Task<PaymentDto> SubmittedPaymentAsync(
        HttpClient analyst, Guid accountId, Guid beneficiaryId, decimal amount)
    {
        var create = await analyst.PostAsJsonAsync("/api/v1/payments",
            new { sourceAccountId = accountId, beneficiaryId, amount, currency = "USD" });
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        return payment;
    }

    private async Task<CaseDto?> FindOpenCaseAsync(HttpClient client, string paymentReference)
    {
        var response = await client.GetAsync("/api/v1/compliance/cases");
        response.EnsureSuccessStatusCode();
        var cases = await response.Content.ReadFromJsonAsync<List<CaseDto>>(Json);
        return cases!.FirstOrDefault(c => c.PaymentReference == paymentReference);
    }

    [Fact]
    public async Task Submitting_a_watchlisted_payment_raises_an_open_hold()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupWatchlistedAsync(analyst, approver);

        var payment = await SubmittedPaymentAsync(analyst, accountId, beneficiaryId, 100m);

        var raised = await FindOpenCaseAsync(analyst, payment.PaymentReference);
        Assert.NotNull(raised);
        Assert.Equal(1, raised!.Status);            // Open
        Assert.Equal(payment.Id, raised.PaymentId);
    }

    [Fact]
    public async Task Compliance_queue_requires_authentication()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/compliance/cases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_read_the_queue_but_cannot_clear()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var (accountId, beneficiaryId) = await SetupWatchlistedAsync(analyst, approver);
        var payment = await SubmittedPaymentAsync(analyst, accountId, beneficiaryId, 100m);
        var raised = await FindOpenCaseAsync(analyst, payment.PaymentReference);

        var read = await auditor.GetAsync("/api/v1/compliance/cases");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var clear = await auditor.PostAsJsonAsync($"/api/v1/compliance/cases/{raised!.Id}/clear", new { notes = "no" });
        Assert.Equal(HttpStatusCode.Forbidden, clear.StatusCode);
    }

    [Fact]
    public async Task Analyst_cannot_clear_a_hold()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupWatchlistedAsync(analyst, approver);
        var payment = await SubmittedPaymentAsync(analyst, accountId, beneficiaryId, 100m);
        var raised = await FindOpenCaseAsync(analyst, payment.PaymentReference);

        var clear = await analyst.PostAsJsonAsync($"/api/v1/compliance/cases/{raised!.Id}/clear", new { notes = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, clear.StatusCode);
    }

    [Fact]
    public async Task Held_payment_cannot_settle_until_cleared()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var compliance = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Compliance);
        var (accountId, beneficiaryId) = await SetupWatchlistedAsync(analyst, approver);

        var payment = await SubmittedPaymentAsync(analyst, accountId, beneficiaryId, 100m);
        await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "ok" });

        // Blocked while the hold is open.
        var blocked = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemLike>(Json);
        Assert.Contains("onComplianceHold", problem!.Type);

        // Clear the hold, then it settles.
        var raised = await FindOpenCaseAsync(analyst, payment.PaymentReference);
        var cleared = await compliance.PostAsJsonAsync(
            $"/api/v1/compliance/cases/{raised!.Id}/clear", new { notes = "reviewed" });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        var settled = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        var dto = await settled.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(5, dto!.Status); // Completed
    }

    [Fact]
    public async Task Rejecting_a_hold_keeps_the_payment_blocked()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var compliance = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Compliance);
        var (accountId, beneficiaryId) = await SetupWatchlistedAsync(analyst, approver);

        var payment = await SubmittedPaymentAsync(analyst, accountId, beneficiaryId, 100m);
        await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "ok" });

        var raised = await FindOpenCaseAsync(analyst, payment.PaymentReference);
        var rejected = await compliance.PostAsJsonAsync(
            $"/api/v1/compliance/cases/{raised!.Id}/reject", new { notes = "confirmed hit" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var stillBlocked = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/process", null);
        Assert.Equal(HttpStatusCode.Conflict, stillBlocked.StatusCode);
    }

    [Fact]
    public async Task Deciding_a_closed_case_again_conflicts()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var compliance = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Compliance);
        var (accountId, beneficiaryId) = await SetupWatchlistedAsync(analyst, approver);
        var payment = await SubmittedPaymentAsync(analyst, accountId, beneficiaryId, 100m);
        var raised = await FindOpenCaseAsync(analyst, payment.PaymentReference);

        var first = await compliance.PostAsJsonAsync($"/api/v1/compliance/cases/{raised!.Id}/clear", new { notes = "ok" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await compliance.PostAsJsonAsync($"/api/v1/compliance/cases/{raised.Id}/reject", new { notes = "no" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Reveal_returns_the_full_number_for_compliance()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var compliance = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Compliance);
        var (accountId, _) = await SetupWatchlistedAsync(analyst, approver);

        var response = await compliance.GetAsync($"/api/v1/accounts/{accountId}/reveal-number");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var full = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("*", full);
    }

    [Fact]
    public async Task A_clean_payment_raises_no_hold()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);

        var cust = await analyst.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Clean Co", countryCode = "US" });
        var customer = await cust.Content.ReadFromJsonAsync<IdHolder>(Json);
        var acc = await analyst.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency = "USD", openingBalance = 1000m, dailyLimit = 100000m });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);
        var ben = await analyst.PostAsJsonAsync("/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Ordinary Payee Ltd", accountNumber = "7778889990001", currency = "USD" });
        var beneficiary = await ben.Content.ReadFromJsonAsync<IdHolder>(Json);
        await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        var payment = await SubmittedPaymentAsync(analyst, account!.Id, beneficiary.Id, 250m);

        var hold = await FindOpenCaseAsync(analyst, payment.PaymentReference);
        Assert.Null(hold);
    }

    private sealed record IdHolder(Guid Id);
    private sealed record PaymentDto(Guid Id, int Status, decimal Amount, string PaymentReference);
    private sealed record CaseDto(Guid Id, Guid PaymentId, string PaymentReference, int Status);
    private sealed record ProblemLike(string Type, string Title);
}
