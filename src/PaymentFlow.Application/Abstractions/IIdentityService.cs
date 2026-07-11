using PaymentFlow.Application.Common;
using PaymentFlow.Application.Features.Auth;

namespace PaymentFlow.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<AuthResultDto>> LoginAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken);
    Task<Result<AuthResultDto>> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task<Result> LogoutAsync(string refreshToken, Guid? userId, string? ipAddress, CancellationToken cancellationToken);
}
