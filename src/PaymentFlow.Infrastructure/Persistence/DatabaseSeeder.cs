using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentFlow.Domain.Constants;
using PaymentFlow.Infrastructure.Identity;

namespace PaymentFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    /// <summary>
    /// Seeds the five roles and, when Seed:DemoPassword is configured, one demo
    /// user per role. All data is fictional; no demo users are created when the
    /// password is absent (e.g. production).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var demoPassword = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogInformation("Seed:DemoPassword not configured; skipping demo users");
            return;
        }

        var demoUsers = new (string Email, string DisplayName, string Role)[]
        {
            ("admin@paymentflow.local", "Ava Admin", Roles.Administrator),
            ("analyst@paymentflow.local", "Omar Analyst", Roles.OperationsAnalyst),
            ("approver@paymentflow.local", "Priya Approver", Roles.PaymentApprover),
            ("compliance@paymentflow.local", "Carlos Compliance", Roles.ComplianceOfficer),
            ("auditor@paymentflow.local", "Aisha Auditor", Roles.ReadOnlyAuditor)
        };

        foreach (var (email, displayName, role) in demoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName
            };

            var result = await userManager.CreateAsync(user, demoPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("Seeded demo user {Email} with role {Role}", email, role);
            }
            else
            {
                logger.LogWarning("Failed to seed {Email}: {Errors}", email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
