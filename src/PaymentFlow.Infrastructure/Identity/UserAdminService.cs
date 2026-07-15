using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Common.Paging;
using PaymentFlow.Application.Features.Admin;
using PaymentFlow.Domain.Constants;
using PaymentFlow.Domain.Entities;
using PaymentFlow.Infrastructure.Persistence;

namespace PaymentFlow.Infrastructure.Identity;

/// <summary>
/// Administrative user &amp; role management over ASP.NET Identity. Enforces the
/// admin guard rails (no self-deactivation, no removing the last administrator's
/// access) and audits every mutation via <see cref="SecurityAuditEvent"/>.
/// </summary>
public sealed class UserAdminService(
    UserManager<ApplicationUser> userManager,
    PaymentFlowDbContext dbContext,
    IDateTimeProvider clock) : IUserAdminService
{
    public async Task<Result<PagedResult<AdminUserDto>>> ListAsync(
        PagedRequest request, CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(term)) || u.DisplayName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip(request.Skip).Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
            items.Add(await ToDtoAsync(user));

        return Result.Success(new PagedResult<AdminUserDto>(items, request.Page, request.PageSize, totalCount));
    }

    public async Task<Result<AdminUserDto>> CreateAsync(
        string email, string displayName, string password, IReadOnlyList<string> roles,
        string? actingUserId, string? actingEmail, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return Result.Failure<AdminUserDto>(
                Error.Validation("user.duplicateEmail", "A user with that email already exists."));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAtUtc = clock.UtcNow
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            return Result.Failure<AdminUserDto>(IdentityError(created));

        if (roles.Count > 0)
        {
            var added = await userManager.AddToRolesAsync(user, roles.Distinct());
            if (!added.Succeeded)
                return Result.Failure<AdminUserDto>(IdentityError(added));
        }

        await AuditAsync(SecurityEventTypes.UserCreated, actingUserId, actingEmail,
            $"Created user {email} with roles [{string.Join(", ", roles)}].", cancellationToken);

        return Result.Success(await ToDtoAsync(user));
    }

    public async Task<Result<AdminUserDto>> SetActiveAsync(
        Guid userId, bool isActive, string? actingUserId, string? actingEmail, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure<AdminUserDto>(Error.NotFound("user.notFound", "User not found."));

        if (!isActive)
        {
            if (actingUserId is not null && string.Equals(actingUserId, userId.ToString(), StringComparison.Ordinal))
                return Result.Failure<AdminUserDto>(
                    Error.Conflict("user.selfDeactivate", "You cannot deactivate your own account."));

            if (await IsLastActiveAdministratorAsync(user))
                return Result.Failure<AdminUserDto>(
                    Error.Conflict("user.lastAdmin", "Cannot deactivate the last active administrator."));
        }

        if (user.IsActive == isActive)
            return Result.Success(await ToDtoAsync(user));

        user.IsActive = isActive;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
            return Result.Failure<AdminUserDto>(IdentityError(updated));

        await AuditAsync(
            isActive ? SecurityEventTypes.UserActivated : SecurityEventTypes.UserDeactivated,
            actingUserId, actingEmail,
            $"{(isActive ? "Activated" : "Deactivated")} user {user.Email}.", cancellationToken);

        return Result.Success(await ToDtoAsync(user));
    }

    public async Task<Result<AdminUserDto>> SetRolesAsync(
        Guid userId, IReadOnlyList<string> roles,
        string? actingUserId, string? actingEmail, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure<AdminUserDto>(Error.NotFound("user.notFound", "User not found."));

        var target = roles.Distinct().ToList();
        var current = await userManager.GetRolesAsync(user);

        var losingAdmin = current.Contains(Roles.Administrator) && !target.Contains(Roles.Administrator);
        if (losingAdmin)
        {
            if (actingUserId is not null && string.Equals(actingUserId, userId.ToString(), StringComparison.Ordinal))
                return Result.Failure<AdminUserDto>(
                    Error.Conflict("user.selfRemoveAdmin", "You cannot remove your own administrator role."));

            if (await IsLastActiveAdministratorAsync(user))
                return Result.Failure<AdminUserDto>(
                    Error.Conflict("user.lastAdmin", "Cannot remove the administrator role from the last active administrator."));
        }

        var toRemove = current.Except(target).ToList();
        var toAdd = target.Except(current).ToList();

        if (toRemove.Count > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removed.Succeeded)
                return Result.Failure<AdminUserDto>(IdentityError(removed));
        }
        if (toAdd.Count > 0)
        {
            var added = await userManager.AddToRolesAsync(user, toAdd);
            if (!added.Succeeded)
                return Result.Failure<AdminUserDto>(IdentityError(added));
        }

        await AuditAsync(SecurityEventTypes.UserRolesChanged, actingUserId, actingEmail,
            $"Roles for {user.Email} set to [{string.Join(", ", target)}].", cancellationToken);

        return Result.Success(await ToDtoAsync(user));
    }

    public async Task<Result> ResetPasswordAsync(
        Guid userId, string newPassword, string? actingUserId, string? actingEmail, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(Error.NotFound("user.notFound", "User not found."));

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded)
            return Result.Failure(IdentityError(reset));

        await AuditAsync(SecurityEventTypes.UserPasswordReset, actingUserId, actingEmail,
            $"Password reset for {user.Email}.", cancellationToken);

        return Result.Success();
    }

    private async Task<bool> IsLastActiveAdministratorAsync(ApplicationUser candidate)
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.Administrator);
        var otherActiveAdmins = admins.Count(a => a.IsActive && a.Id != candidate.Id);
        return otherActiveAdmins == 0;
    }

    private async Task<AdminUserDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AdminUserDto(
            user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive,
            roles.ToList(), user.CreatedAtUtc);
    }

    private async Task AuditAsync(
        string eventType, string? actingUserId, string? actingEmail, string details, CancellationToken cancellationToken)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = Guid.TryParse(actingUserId, out var id) ? id : null,
            Email = actingEmail,
            EventType = eventType,
            Succeeded = true,
            Details = details,
            OccurredAtUtc = clock.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Error IdentityError(IdentityResult result)
        => Error.Validation("user.identityError",
            string.Join("; ", result.Errors.Select(e => e.Description)));
}
