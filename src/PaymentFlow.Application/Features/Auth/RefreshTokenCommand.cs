using FluentValidation;
using MediatR;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Application.Features.Auth;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress)
    : IRequest<Result<AuthResultDto>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class RefreshTokenCommandHandler(IIdentityService identityService)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResultDto>>
{
    public Task<Result<AuthResultDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        => identityService.RefreshAsync(request.RefreshToken, request.IpAddress, cancellationToken);
}
