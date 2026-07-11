using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class BeneficiariesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<Guid> FirstCustomerIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/customers?pageSize=1");
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CustomerSummary>>(Json);
        return payload!.Items[0].Id;
    }

    [Fact]
    public async Task Full_beneficiary_lifecycle_draft_submit_approve()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var customerId = await FirstCustomerIdAsync(analyst);

        var create = await analyst.PostAsJsonAsync("/api/v1/beneficiaries", new
        {
            customerId,
            name = "New Supplier Ltd",
            accountNumber = "5544332211009",
            bankName = "Test Bank",
            currency = "USD"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var beneficiary = await create.Content.ReadFromJsonAsync<BeneficiaryDto>(Json);
        Assert.Equal(1, beneficiary!.Status); // Draft
        Assert.StartsWith("****", beneficiary.MaskedNumber[..4]);

        var submit = await analyst.PostAsync($"/api/v1/beneficiaries/{beneficiary.Id}/submit-for-approval", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        // Analyst cannot approve.
        var analystApprove = await analyst.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, analystApprove.StatusCode);

        // Approver can.
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var approve = await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary.Id}/approve", new { notes = "ok" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<BeneficiaryDto>(Json);
        Assert.Equal(3, approved!.Status); // Approved
    }

    [Fact]
    public async Task Approving_a_draft_beneficiary_conflicts()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);
        var customerId = await FirstCustomerIdAsync(analyst);

        var create = await analyst.PostAsJsonAsync("/api/v1/beneficiaries", new
        {
            customerId, name = "Draft Only", accountNumber = "1112223334445", currency = "EUR"
        });
        var beneficiary = await create.Content.ReadFromJsonAsync<BeneficiaryDto>(Json);

        // Skip submit, approve directly -> invalid transition -> 409.
        var approve = await approver.PostAsJsonAsync($"/api/v1/beneficiaries/{beneficiary!.Id}/approve", new { notes = "x" });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    private sealed record PagedResponse<T>(List<T> Items);
    private sealed record CustomerSummary(Guid Id);
    private sealed record BeneficiaryDto(Guid Id, string MaskedNumber, int Status);
}
