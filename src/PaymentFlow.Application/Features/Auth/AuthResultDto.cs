namespace PaymentFlow.Application.Features.Auth;

public sealed record AuthResultDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    AuthUserDto User);

public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
