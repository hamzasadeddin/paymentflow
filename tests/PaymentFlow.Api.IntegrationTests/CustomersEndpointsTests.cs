using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class CustomersEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task List_customers_returns_paged_seed_data()
    {
        var client = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var response = await client.GetAsync("/api/v1/customers?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CustomerSummary>>(Json);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 5);
        Assert.All(payload.Items, c => Assert.False(string.IsNullOrWhiteSpace(c.CustomerReference)));
    }

    [Fact]
    public async Task Search_filters_customers()
    {
        var client = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var response = await client.GetAsync("/api/v1/customers?search=Cedar");
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CustomerSummary>>(Json);
        Assert.Contains(payload!.Items, c => c.Name.Contains("Cedar"));
    }

    [Fact]
    public async Task Analyst_can_create_customer_and_account_number_is_masked()
    {
        var client = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);

        var create = await client.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Test Person", email = "test.person@example.com", countryCode = "US" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var customer = await create.Content.ReadFromJsonAsync<CustomerDetail>(Json);

        var addAccount = await client.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency = "USD", openingBalance = 1000.50m, dailyLimit = 500m });
        Assert.Equal(HttpStatusCode.Created, addAccount.StatusCode);
        var account = await addAccount.Content.ReadFromJsonAsync<AccountSummary>(Json);

        // Masked form is any number of stars followed by exactly the last 4 digits.
        Assert.Matches(@"^\*+\d{4}$", account!.MaskedNumber);
    }

    [Fact]
    public async Task Auditor_cannot_create_customer()
    {
        var client = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var response = await client.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Blocked", countryCode = "US" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_read_customers()
    {
        var client = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var response = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_stale_rowversion_conflicts()
    {
        var client = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var create = await client.PostAsJsonAsync("/api/v1/customers",
            new { type = 2, name = "Concurrency Co", countryCode = "US" });
        var customer = await create.Content.ReadFromJsonAsync<CustomerDetail>(Json);

        // First update succeeds and changes the row version.
        var firstUpdate = await client.PutAsJsonAsync($"/api/v1/customers/{customer!.Id}",
            new { name = "Concurrency Co v2", status = 1, rowVersion = customer.RowVersion });
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        // Second update reuses the original (now stale) row version.
        var staleUpdate = await client.PutAsJsonAsync($"/api/v1/customers/{customer.Id}",
            new { name = "Concurrency Co v3", status = 1, rowVersion = customer.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    private sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record CustomerSummary(Guid Id, string CustomerReference, string Name);
    private sealed record CustomerDetail(Guid Id, string Name, string RowVersion);
    private sealed record AccountSummary(Guid Id, string MaskedNumber, string Currency);
}
