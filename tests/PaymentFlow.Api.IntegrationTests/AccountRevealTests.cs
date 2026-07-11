using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class AccountRevealTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/v1/customers",
            new { type = 1, name = "Reveal Target", countryCode = "US" });
        var customer = await create.Content.ReadFromJsonAsync<IdHolder>(Json);
        var acc = await client.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/accounts",
            new { currency = "USD", openingBalance = 10m, dailyLimit = 10m });
        var account = await acc.Content.ReadFromJsonAsync<IdHolder>(Json);
        return account!.Id;
    }

    [Fact]
    public async Task Compliance_officer_can_reveal_number()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var accountId = await CreateAccountAsync(analyst);

        var compliance = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Compliance);
        var reveal = await compliance.GetAsync($"/api/v1/accounts/{accountId}/reveal-number");
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
    }

    [Fact]
    public async Task Analyst_cannot_reveal_number()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var accountId = await CreateAccountAsync(analyst);

        var reveal = await analyst.GetAsync($"/api/v1/accounts/{accountId}/reveal-number");
        Assert.Equal(HttpStatusCode.Forbidden, reveal.StatusCode);
    }

    private sealed record IdHolder(Guid Id);
}
