using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class AdminRulesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Non_admin_cannot_read_rules()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var response = await analyst.GetAsync("/api/v1/admin/rules");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_rules_returns_all_four_sections()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var response = await admin.GetAsync("/api/v1/admin/rules");
        response.EnsureSuccessStatusCode();

        var root = await ReadJsonAsync(response);
        foreach (var section in new[] { "approval", "screening", "reconciliation", "processing" })
        {
            Assert.True(root.TryGetProperty(section, out var s), $"missing section {section}");
            Assert.True(s.TryGetProperty("values", out _));
            Assert.True(s.TryGetProperty("isOverridden", out _));
        }
    }

    [Fact]
    public async Task Invalid_approval_rules_are_rejected()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        // Dual-approval threshold below the auto-approve threshold is invalid.
        var response = await admin.PutAsJsonAsync("/api/v1/admin/rules/approval",
            new { autoApproveBelow = 5000m, dualApprovalAtOrAbove = 1000m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Updating_screening_round_trips_and_audits()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);

        var put = await admin.PutAsJsonAsync("/api/v1/admin/rules/screening", new
        {
            autoScreenOnSubmit = true,
            watchlistBeneficiaryNames = new[] { "Gulf Freight" },
            watchlistCountryCodes = new[] { "IR" },
            singlePaymentReviewLimit = 1234m
        });
        put.EnsureSuccessStatusCode();

        var rules = await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/rules"));
        var screening = rules.GetProperty("screening");
        Assert.True(screening.GetProperty("isOverridden").GetBoolean());
        Assert.Equal(1234m, screening.GetProperty("values").GetProperty("singlePaymentReviewLimit").GetDecimal());

        // The change is itself audited.
        var audit = await ReadJsonAsync(await admin.GetAsync("/api/v1/audit-events?eventType=RuleSetUpdated"));
        Assert.True(audit.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Stale_row_version_is_a_conflict()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);

        object Body(string? rowVersion) => new
        {
            autoProcessEnabled = false, pollingIntervalSeconds = 5, batchSize = 10,
            simulatedLatencyMsMin = 0, simulatedLatencyMsMax = 0, failOnCents = 13, rowVersion
        };

        // First write creates the override row.
        var first = await ReadJsonAsync(await admin.PutAsJsonAsync("/api/v1/admin/rules/processing", Body(null)));
        var staleVersion = first.GetProperty("rowVersion").GetString();

        // A second write on that version succeeds and rotates the version.
        var second = await admin.PutAsJsonAsync("/api/v1/admin/rules/processing", Body(staleVersion));
        second.EnsureSuccessStatusCode();

        // Re-using the now-stale version conflicts.
        var third = await admin.PutAsJsonAsync("/api/v1/admin/rules/processing", Body(staleVersion));
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
    }

    [Fact]
    public async Task Lowering_the_approval_band_auto_approves_a_payment_end_to_end()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);

        // A normal (non-watchlisted) beneficiary so screening stays clear.
        var customer = await CreateAsync<IdHolder>(analyst, "/api/v1/customers",
            new { type = 1, name = "Rules Test", countryCode = "US" });
        var account = await CreateAsync<IdHolder>(analyst, $"/api/v1/customers/{customer.Id}/accounts",
            new { currency = "USD", openingBalance = 10000m, dailyLimit = 100000m });
        var beneficiary = await CreateAsync<IdHolder>(analyst, "/api/v1/beneficiaries",
            new { customerId = customer.Id, name = "Acme Corp", accountNumber = "5550001112223", currency = "USD" });
        await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary.Id}/submit-for-approval", null);
        await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });

        // Widen the auto-approve band so a small payment clears on submit.
        var put = await admin.PutAsJsonAsync("/api/v1/admin/rules/approval",
            new { autoApproveBelow = 100000m, dualApprovalAtOrAbove = 200000m });
        put.EnsureSuccessStatusCode();

        var payment = await CreateAsync<PaymentRow>(analyst, "/api/v1/payments",
            new { sourceAccountId = account.Id, beneficiaryId = beneficiary.Id, amount = 100m, currency = "USD" });
        var submit = await analyst.PostAsync($"/api/v1/payments/{payment.Id}/submit-for-approval", null);
        submit.EnsureSuccessStatusCode();

        var submitted = await submit.Content.ReadFromJsonAsync<PaymentRow>(Json);
        Assert.Equal(3, submitted!.Status); // Approved (auto), not PendingApproval (2)

        // Reset the band so other tests in this class see the pinned defaults.
        await admin.PutAsJsonAsync("/api/v1/admin/rules/approval",
            new { autoApproveBelow = 0m, dualApprovalAtOrAbove = 5000m });
    }

    private async Task<T> CreateAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private sealed record IdHolder(Guid Id);
    private sealed record PaymentRow(Guid Id, int Status, decimal Amount, string PaymentReference);
}
