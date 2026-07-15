using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class AuditEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Audit_events_require_authentication()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_read_the_audit_trail()
    {
        // Logging in as the auditor itself writes a LoginSucceeded event.
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);

        var response = await auditor.GetAsync("/api/v1/audit-events?eventType=LoginSucceeded");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedAudit>(Json);
        Assert.NotNull(page);
        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, e => Assert.Equal("LoginSucceeded", e.EventType));
    }

    [Fact]
    public async Task Plain_analyst_cannot_read_the_audit_trail()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var response = await analyst.GetAsync("/api/v1/audit-events");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Event_type_catalog_includes_administration_group()
    {
        var admin = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Admin);
        var response = await admin.GetAsync("/api/v1/audit-events/event-types");
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<List<EventTypeGroup>>(Json);
        Assert.NotNull(groups);
        Assert.Contains(groups!, g => g.Group == "Administration" && g.Types.Contains("RuleSetUpdated"));
    }

    private sealed record PagedAudit(List<AuditRow> Items, int Page, int PageSize, int TotalCount);
    private sealed record AuditRow(Guid Id, string EventType, bool Succeeded, string? Email);
    private sealed record EventTypeGroup(string Group, List<string> Types);
}
