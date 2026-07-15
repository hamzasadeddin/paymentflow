using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Application.Features.Admin;

namespace PaymentFlow.Application.Abstractions;

/// <summary>
/// Administrative user &amp; role management over the ASP.NET Identity store. Kept
/// behind an abstraction (like <see cref="IIdentityService"/>) so the Application
/// layer stays free of Identity types. Implementations enforce the admin guard
/// rails (no self-deactivation, no removing the last administrator) and write a
/// <c>SecurityAuditEvent</c> for every mutation, so administration is itself
/// audited and shows up in the Phase 07 audit-log viewer.
/// </summary>
public interface IUserAdminService
{
    Task<Result<PagedResult<AdminUserDto>>> ListAsync(PagedRequest request, CancellationToken cancellationToken);

    Task<Result<AdminUserDto>> CreateAsync(
        string email, string displayName, string password, IReadOnlyList<string> roles,
        string? actingUserId, string? actingEmail, CancellationToken cancellationToken);

    Task<Result<AdminUserDto>> SetActiveAsync(
        Guid userId, bool isActive, string? actingUserId, string? actingEmail, CancellationToken cancellationToken);

    Task<Result<AdminUserDto>> SetRolesAsync(
        Guid userId, IReadOnlyList<string> roles,
        string? actingUserId, string? actingEmail, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(
        Guid userId, string newPassword,
        string? actingUserId, string? actingEmail, CancellationToken cancellationToken);
}
