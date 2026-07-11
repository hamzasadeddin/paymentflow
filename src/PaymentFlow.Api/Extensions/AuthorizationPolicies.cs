using Microsoft.AspNetCore.Authorization;
using PaymentFlow.Domain.Constants;

namespace PaymentFlow.Api.Extensions;

/// <summary>
/// Central policy definitions so controllers reference intent ("CanManageCustomers")
/// rather than scattering role lists across attributes.
/// </summary>
public static class AuthorizationPolicies
{
    public const string CanReadOperations = "CanReadOperations";
    public const string CanManageCustomers = "CanManageCustomers";
    public const string CanManageBeneficiaries = "CanManageBeneficiaries";
    public const string CanApproveBeneficiaries = "CanApproveBeneficiaries";
    public const string CanRevealAccountNumbers = "CanRevealAccountNumbers";
    public const string CanManagePayments = "CanManagePayments";
    public const string CanApprovePayments = "CanApprovePayments";

    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Everyone authenticated (including Read-Only Auditor) can read.
            .AddPolicy(CanReadOperations, p => p.RequireRole(
                Roles.Administrator, Roles.OperationsAnalyst, Roles.PaymentApprover,
                Roles.ComplianceOfficer, Roles.ReadOnlyAuditor))
            .AddPolicy(CanManageCustomers, p => p.RequireRole(
                Roles.Administrator, Roles.OperationsAnalyst))
            .AddPolicy(CanManageBeneficiaries, p => p.RequireRole(
                Roles.Administrator, Roles.OperationsAnalyst))
            .AddPolicy(CanApproveBeneficiaries, p => p.RequireRole(
                Roles.Administrator, Roles.PaymentApprover))
            .AddPolicy(CanRevealAccountNumbers, p => p.RequireRole(
                Roles.Administrator, Roles.ComplianceOfficer))
            .AddPolicy(CanManagePayments, p => p.RequireRole(
                Roles.Administrator, Roles.OperationsAnalyst))
            .AddPolicy(CanApprovePayments, p => p.RequireRole(
                Roles.Administrator, Roles.PaymentApprover));

        return services;
    }
}
