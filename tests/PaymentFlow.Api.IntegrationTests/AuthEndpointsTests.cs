using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AdminEmail = "admin@paymentflow.local";

    private sealed record AuthResponse(
        string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, UserInfo User);
    private sealed record UserInfo(Guid Id, string Email, string DisplayName, string[] Roles);

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = ApiFactory.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.Contains("Administrator", auth.User.Roles);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_problem_details()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = "Wrong!Passw0rd" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Login_with_invalid_payload_returns_400()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "not-an-email", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_rejects_replay()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = ApiFactory.DemoPassword });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = auth!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(auth.RefreshToken, rotated!.RefreshToken);

        // Replaying the original (now revoked) token must fail.
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Me_requires_authentication_and_returns_identity()
    {
        var client = factory.CreateClient();

        var anonymous = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = ApiFactory.DemoPassword });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
