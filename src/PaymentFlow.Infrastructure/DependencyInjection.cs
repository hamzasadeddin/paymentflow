using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Infrastructure.Approvals;
using PaymentFlow.Infrastructure.Compliance;
using PaymentFlow.Infrastructure.Configuration;
using PaymentFlow.Infrastructure.Identity;
using PaymentFlow.Infrastructure.Persistence;
using PaymentFlow.Infrastructure.Processing;
using PaymentFlow.Infrastructure.Reconciliation;

namespace PaymentFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentFlowDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure()));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<PaymentFlowDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 64)
            throw new InvalidOperationException(
                "Jwt:Secret must be configured with at least 64 characters (user secrets or environment variable).");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep raw claim names ("sub", "role")
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                // WebSockets can't send an Authorization header on the handshake,
                // so SignalR clients pass the token as a query-string parameter.
                // Accept it only for hub endpoints.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        // Phase 07 — admin-editable rule store, read behind IRuleSettingsProvider
        // (effective = stored override, else the config-bound fallback below).
        services.AddScoped<IRuleSettingsProvider, RuleSettingsProvider>();

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<PaymentFlowDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.Configure<ApprovalPolicyOptions>(
            configuration.GetSection(ApprovalPolicyOptions.SectionName));
        services.AddScoped<IApprovalPolicyProvider, ApprovalPolicyProvider>();

        // Phase 05 — simulated payment processing.
        services.Configure<ProcessingOptions>(
            configuration.GetSection(ProcessingOptions.SectionName));
        services.AddScoped<ISettlementSimulator, DeterministicSettlementSimulator>();
        services.AddHostedService<PaymentProcessingWorker>();

        // Phase 06 — compliance & reconciliation.
        services.Configure<ScreeningOptions>(
            configuration.GetSection(ScreeningOptions.SectionName));
        services.AddScoped<IComplianceScreeningService, RuleBasedComplianceScreeningService>();
        services.Configure<ReconciliationOptions>(
            configuration.GetSection(ReconciliationOptions.SectionName));
        services.AddScoped<IExternalStatementProvider, SimulatedStatementProvider>();

        return services;
    }
}
