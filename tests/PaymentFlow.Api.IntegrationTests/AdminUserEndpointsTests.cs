using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class AdminUserEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static object NewUser(string email, params string[] roles) =>
        new { email, displayName = "Test User", password = ApiFactory.DemoPassword, roles };

    [Fact]
    public async Task Non_admin_cannot_list_users()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var response = await analyst.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_seeded_users()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var response = await admin.GetAsync("/api/v1/admin/users?pageSize=100");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedUsers>(Json);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, u => u.Email == AuthHelper.Admin);
    }

    [Fact]
    public async Task Created_user_can_log_in()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var email = $"created-{Guid.NewGuid():N}@paymentflow.local";

        var create = await admin.PostAsJsonAsync("/api/v1/admin/users", NewUser(email, "OperationsAnalyst"));
        create.EnsureSuccessStatusCode();

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = ApiFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var email = $"deact-{Guid.NewGuid():N}@paymentflow.local";

        var create = await admin.PostAsJsonAsync("/api/v1/admin/users", NewUser(email, "OperationsAnalyst"));
        var created = await create.Content.ReadFromJsonAsync<UserRow>(Json);

        var deactivate = await admin.PostAsync($"/api/v1/admin/users/{created!.Id}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = ApiFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_their_own_account()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);

        var list = await admin.GetAsync("/api/v1/admin/users?pageSize=100");
        var page = await list.Content.ReadFromJsonAsync<PagedUsers>(Json);
        var self = page!.Items.First(u => u.Email == AuthHelper.Admin);

        var response = await admin.PostAsync($"/api/v1/admin/users/{self.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Role_change_is_reflected_in_the_user_list()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var email = $"roles-{Guid.NewGuid():N}@paymentflow.local";

        var create = await admin.PostAsJsonAsync("/api/v1/admin/users", NewUser(email, "OperationsAnalyst"));
        var created = await create.Content.ReadFromJsonAsync<UserRow>(Json);

        var change = await admin.PutAsJsonAsync($"/api/v1/admin/users/{created!.Id}/roles",
            new { roles = new[] { "PaymentApprover", "ComplianceOfficer" } });
        change.EnsureSuccessStatusCode();

        var updated = await change.Content.ReadFromJsonAsync<UserRow>(Json);
        Assert.Contains("PaymentApprover", updated!.Roles);
        Assert.Contains("ComplianceOfficer", updated.Roles);
        Assert.DoesNotContain("OperationsAnalyst", updated.Roles);
    }

    private sealed record PagedUsers(List<UserRow> Items, int TotalCount);
    private sealed record UserRow(Guid Id, string Email, string DisplayName, bool IsActive, List<string> Roles);
}
