using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PaymentFlow.Api.IntegrationTests;

internal static class AuthHelper
{
    private sealed record AuthResponse(string AccessToken);

    public static async Task<HttpClient> AuthenticatedClientAsync(ApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = ApiFactory.DemoPassword });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public const string Admin = "admin@paymentflow.local";
    public const string Analyst = "analyst@paymentflow.local";
    public const string Approver = "approver@paymentflow.local";
    public const string Compliance = "compliance@paymentflow.local";
    public const string Auditor = "auditor@paymentflow.local";
}
