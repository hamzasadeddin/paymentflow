using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentFlow.Infrastructure.Persistence;
using Xunit;

namespace PaymentFlow.Api.IntegrationTests;

/// <summary>
/// Boots the real API pipeline against SQLite in-memory so integration tests
/// need no SQL Server. Roles and demo users are seeded once per factory.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo!Passw0rd1";
    private SqliteConnection _connection = default!;

    static ApiFactory()
    {
        // AddInfrastructure(builder.Configuration) reads Jwt:* while the entry point
        // runs — before WebApplicationFactory applies ConfigureAppConfiguration below.
        // Environment variables are picked up by WebApplication.CreateBuilder at
        // builder-creation time, so they arrive early enough for the JWT guard.
        // ("__" maps to the ":" configuration separator.)
        Environment.SetEnvironmentVariable("Jwt__Secret",
            "integration-test-jwt-secret-0123456789-0123456789-0123456789-0123456789");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "PaymentFlow");
        Environment.SetEnvironmentVariable("Jwt__Audience", "PaymentFlow.Web");
        Environment.SetEnvironmentVariable("Seed__DemoPassword", DemoPassword);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "integration-test-jwt-secret-0123456789-0123456789-0123456789-0123456789",
                ["Jwt:Issuer"] = "PaymentFlow",
                ["Jwt:Audience"] = "PaymentFlow.Web",
                ["Seed:DemoPassword"] = DemoPassword
            }));

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
        d.ServiceType == typeof(DbContextOptions<PaymentFlowDbContext>) ||
        d.ServiceType == typeof(DbContextOptions) ||
        d.ServiceType == typeof(PaymentFlowDbContext) ||
        (d.ServiceType.FullName?.Contains("DbContextOptionsConfiguration") ?? false))
        .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            services.AddDbContext<PaymentFlowDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentFlowDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
        await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
