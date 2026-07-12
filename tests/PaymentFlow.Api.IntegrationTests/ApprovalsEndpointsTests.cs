using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

/// <summary>
/// Phase 04 maker-checker engine: separation of duties, dual control, and the
/// unified approvals queue. The harness pins AutoApproveBelow=0 and
/// DualApprovalAtOrAbove=5000 (see <see cref="ApiFactory"/>).
/// </summary>
public class ApprovalsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<(Guid AccountId, Guid BeneficiaryId)> SetupAsync(
        HttpClient maker, HttpClient approver, string currency = "USD",
        decimal openingBalance = 100000m, decimal dailyLimit = 100000m)
    {
        var cust = await maker.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Approval Test", countryCode = "US" });
        var customer = await cust.Content.ReadFromJsonAsync<IdHolder>(Json);

        var acc = await maker.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency, openingBalance, dailyLimit });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);

        var ben = await maker.PostAsJsonAsync("/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Payee Co", accountNumber = "9990001112223", currency });
        var beneficiary = await ben.Content.ReadFromJsonAsync<IdHolder>(Json);

        await maker.PostAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        return (account!.Id, beneficiary.Id);
    }

    private static Task<HttpResponseMessage> CreatePayment(
        HttpClient client, Guid accountId, Guid beneficiaryId, decimal amount, string currency = "USD")
        => client.PostAsJsonAsync("/api/v1/payments",
            new { sourceAccountId = accountId, beneficiaryId, amount, currency });

    [Fact]
    public async Task Maker_cannot_approve_their_own_payment()
    {
        // Admin is in both the manage and approve policies, so the block must be
        // enforced at the identity level (maker != checker), not just by role.
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(admin, approver);

        var create = await CreatePayment(admin, accountId, beneficiaryId, 1200m);
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        await admin.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);

        var selfApprove = await admin.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "me" });
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);
    }

    [Fact]
    public async Task Single_band_payment_is_approved_by_one_checker()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var create = await CreatePayment(analyst, accountId, beneficiaryId, 1200m);
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        // RequiredApprovals is stamped at submit time, not at create.
        var submit = await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        var submitted = await submit.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(1, submitted!.RequiredApprovals); // single-approval band

        var approve = await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "ok" });
        var approved = await approve.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(3, approved!.Status); // Approved after one checker
    }

    [Fact]
    public async Task Dual_control_requires_two_distinct_approvers()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        // >= 5000 => dual approval.
        var create = await CreatePayment(analyst, accountId, beneficiaryId, 6000m);
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);

        // RequiredApprovals is stamped at submit time, not at create.
        var submit = await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        var submitted = await submit.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(2, submitted!.RequiredApprovals);

        // First approval: still pending.
        var first = await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "1" });
        var afterFirst = await first.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(2, afterFirst!.Status); // still PendingApproval

        // Same approver can't approve twice.
        var repeat = await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "again" });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);

        // A distinct approver finalizes.
        var second = await admin.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "2" });
        var afterSecond = await second.Content.ReadFromJsonAsync<PaymentDto>(Json);
        Assert.Equal(3, afterSecond!.Status); // Approved
    }

    [Fact]
    public async Task Funds_are_reserved_only_on_final_approval()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var before = await analyst.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{accountId}", Json);

        var create = await CreatePayment(analyst, accountId, beneficiaryId, 6000m);
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "1" });

        // After the first (partial) approval, funds must NOT yet be reserved.
        var mid = await analyst.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{accountId}", Json);
        Assert.Equal(before!.AvailableBalance, mid!.AvailableBalance);

        await admin.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new { notes = "2" });

        var after = await analyst.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{accountId}", Json);
        Assert.Equal(before.AvailableBalance - 6000m, after!.AvailableBalance);
    }

    [Fact]
    public async Task Approval_queue_lists_pending_payment_with_progress()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var (accountId, beneficiaryId) = await SetupAsync(analyst, approver);

        var create = await CreatePayment(analyst, accountId, beneficiaryId, 1200m);
        var payment = await create.Content.ReadFromJsonAsync<PaymentDto>(Json);
        await analyst.PostAsync($"/api/v1/payments/{payment!.Id}/submit-for-approval", null);

        var queue = await approver.GetFromJsonAsync<ApprovalQueueResponse>("/api/v1/approvals", Json);
        var item = queue!.Payments.FirstOrDefault(p => p.SubjectId == payment.Id);

        Assert.NotNull(item);
        Assert.Equal(1, item!.RequiredApprovals);
        Assert.Equal(0, item.ApprovalsReceived);
    }

    [Fact]
    public async Task Auditor_can_read_the_queue_but_not_approve()
    {
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);

        var queue = await auditor.GetAsync("/api/v1/approvals");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);

        var approve = await auditor.PostAsJsonAsync($"/api/v1/payments/{Guid.NewGuid()}/approve", new { notes = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    private sealed record IdHolder(Guid Id);
    private sealed record PaymentDto(Guid Id, int Status, decimal Amount, int RequiredApprovals);
    private sealed record AccountDto(decimal AvailableBalance, decimal LedgerBalance);
    private sealed record ApprovalQueueResponse(List<QueueItem> Payments, List<QueueItem> Beneficiaries);
    private sealed record QueueItem(Guid SubjectId, int RequiredApprovals, int ApprovalsReceived);
}
