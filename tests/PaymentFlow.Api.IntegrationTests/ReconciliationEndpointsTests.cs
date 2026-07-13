using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

public class ReconciliationEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<RunDto> RunAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/reconciliation/run", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
    }

    private async Task<List<BreakDto>> BreaksAsync(HttpClient client, Guid runId)
    {
        var response = await client.GetAsync($"/api/v1/reconciliation/runs/{runId}/breaks");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<BreakDto>>(Json))!;
    }

    [Fact]
    public async Task Run_produces_a_run_with_breaks_from_seeded_data()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);

        var run = await RunAsync(analyst);
        Assert.False(string.IsNullOrWhiteSpace(run.RunReference));
        Assert.StartsWith("RECON-", run.RunReference);

        // Seeded completed payments + synthetic drift yield at least one break.
        Assert.True(run.BreakCount >= 1);

        var breaks = await BreaksAsync(analyst, run.Id);
        Assert.Equal(run.BreakCount, breaks.Count);
    }

    [Fact]
    public async Task Run_requires_the_reconcile_role()
    {
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var approver = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Approver);

        var auditorRun = await auditor.PostAsync("/api/v1/reconciliation/run", null);
        Assert.Equal(HttpStatusCode.Forbidden, auditorRun.StatusCode);

        var approverRun = await approver.PostAsync("/api/v1/reconciliation/run", null);
        Assert.Equal(HttpStatusCode.Forbidden, approverRun.StatusCode);
    }

    [Fact]
    public async Task Run_is_unauthorized_without_a_token()
    {
        var anon = factory.CreateClient();
        var response = await anon.PostAsync("/api/v1/reconciliation/run", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_read_runs()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        await RunAsync(analyst);

        var response = await auditor.GetAsync("/api/v1/reconciliation/runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var runs = await response.Content.ReadFromJsonAsync<List<RunDto>>(Json);
        Assert.NotEmpty(runs!);
    }

    [Fact]
    public async Task Resolving_a_break_succeeds_and_re_resolving_conflicts()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var run = await RunAsync(analyst);
        var breaks = await BreaksAsync(analyst, run.Id);
        var target = breaks.First();

        var first = await analyst.PostAsJsonAsync(
            $"/api/v1/reconciliation/breaks/{target.Id}/resolve", new { notes = "worked" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var resolved = await first.Content.ReadFromJsonAsync<BreakDto>(Json);
        Assert.Equal(2, resolved!.Status); // Resolved

        var second = await analyst.PostAsJsonAsync(
            $"/api/v1/reconciliation/breaks/{target.Id}/resolve", new { notes = "again" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Ignoring_a_break_succeeds()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var run = await RunAsync(analyst);
        var breaks = await BreaksAsync(analyst, run.Id);
        var target = breaks.First();

        var response = await analyst.PostAsJsonAsync(
            $"/api/v1/reconciliation/breaks/{target.Id}/ignore", new { notes = "known difference" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ignored = await response.Content.ReadFromJsonAsync<BreakDto>(Json);
        Assert.Equal(3, ignored!.Status); // Ignored
    }

    [Fact]
    public async Task Resolving_a_break_requires_the_reconcile_role()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var auditor = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Auditor);
        var run = await RunAsync(analyst);
        var breaks = await BreaksAsync(analyst, run.Id);

        var response = await auditor.PostAsJsonAsync(
            $"/api/v1/reconciliation/breaks/{breaks.First().Id}/resolve", new { notes = "no" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Breaks_for_an_unknown_run_return_not_found()
    {
        var analyst = await AuthHelper.AuthenticatedClientAsync(factory, AuthHelper.Analyst);
        var response = await analyst.GetAsync($"/api/v1/reconciliation/runs/{Guid.NewGuid()}/breaks");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record RunDto(Guid Id, string RunReference, int MatchedCount, int BreakCount);
    private sealed record BreakDto(Guid Id, Guid RunId, int Type, int Status);
}
