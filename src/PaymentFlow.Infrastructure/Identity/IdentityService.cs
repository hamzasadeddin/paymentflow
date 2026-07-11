using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Features.Auth;
using PaymentFlow.Domain.Entities;
using PaymentFlow.Infrastructure.Persistence;

namespace PaymentFlow.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService tokenService,
    PaymentFlowDbContext dbContext,
    IOptions<JwtOptions> jwtOptions) : IIdentityService
{
    // Deliberately identical message for unknown user / wrong password / inactive
    // account, so the endpoint cannot be used to enumerate valid emails.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalidCredentials", "Invalid email or password.");

    private static readonly Error LockedOut =
        Error.Unauthorized("auth.lockedOut", "Account is temporarily locked. Try again later.");

    private static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalidRefreshToken", "The refresh token is invalid or expired.");

    public async Task<Result<AuthResultDto>> LoginAsync(
        string email, string password, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            await AuditAsync(SecurityEventTypes.LoginFailed, false, null, email, ipAddress,
                user is null ? "Unknown email" : "Inactive account", cancellationToken);
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            await AuditAsync(SecurityEventTypes.AccountLockedOut, false, user.Id, email, ipAddress, null, cancellationToken);
            return Result.Failure<AuthResultDto>(LockedOut);
        }

        if (!check.Succeeded)
        {
            await AuditAsync(SecurityEventTypes.LoginFailed, false, user.Id, email, ipAddress, "Wrong password", cancellationToken);
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        var authResult = await IssueTokensAsync(user, ipAddress, cancellationToken);
        await AuditAsync(SecurityEventTypes.LoginSucceeded, true, user.Id, email, ipAddress, null, cancellationToken);
        return Result.Success(authResult);
    }

    public async Task<Result<AuthResultDto>> RefreshAsync(
        string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var hash = tokenService.Hash(refreshToken);
        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
            return Result.Failure<AuthResultDto>(InvalidRefreshToken);

        if (!stored.IsActive(DateTime.UtcNow))
        {
            // A rotated token being presented again suggests theft; flag it.
            await AuditAsync(SecurityEventTypes.TokenReplayDetected, false, stored.UserId, null, ipAddress, null, cancellationToken);
            return Result.Failure<AuthResultDto>(InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result.Failure<AuthResultDto>(InvalidRefreshToken);

        var authResult = await IssueTokensAsync(user, ipAddress, cancellationToken, rotatedFrom: stored);
        await AuditAsync(SecurityEventTypes.TokenRefreshed, true, user.Id, user.Email, ipAddress, null, cancellationToken);
        return Result.Success(authResult);
    }

    public async Task<Result> LogoutAsync(
        string refreshToken, Guid? userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var hash = tokenService.Hash(refreshToken);
        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is not null && stored.RevokedAtUtc is null)
            stored.Revoke(DateTime.UtcNow);

        await AuditAsync(SecurityEventTypes.Logout, true, userId ?? stored?.UserId, null, ipAddress, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<AuthResultDto> IssueTokensAsync(
        ApplicationUser user, string? ipAddress, CancellationToken cancellationToken, RefreshToken? rotatedFrom = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles);

        var rawRefreshToken = tokenService.CreateRefreshToken();
        var newTokenHash = tokenService.Hash(rawRefreshToken);

        rotatedFrom?.Revoke(DateTime.UtcNow, newTokenHash);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = ipAddress
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(
            accessToken,
            expiresAtUtc,
            rawRefreshToken,
            new AuthUserDto(user.Id, user.Email ?? string.Empty, user.DisplayName, roles.ToList()));
    }

    private async Task AuditAsync(
        string eventType, bool succeeded, Guid? userId, string? email, string? ipAddress,
        string? details, CancellationToken cancellationToken)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            EventType = eventType,
            Succeeded = succeeded,
            UserId = userId,
            Email = email,
            IpAddress = ipAddress,
            Details = details
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
