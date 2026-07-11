using FluentValidation;
using MediatR;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Application.Features.Auth;

public sealed record LogoutCommand(string RefreshToken, Guid? UserId, string? IpAddress)
    : IRequest<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class LogoutCommandHandler(IIdentityService identityService)
    : IRequestHandler<LogoutCommand, Result>
{
    public Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
        => identityService.LogoutAsync(request.RefreshToken, request.UserId, request.IpAddress, cancellationToken);
}
