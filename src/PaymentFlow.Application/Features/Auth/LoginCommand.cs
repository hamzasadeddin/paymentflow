using FluentValidation;
using MediatR;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password, string? IpAddress)
    : IRequest<Result<AuthResultDto>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class LoginCommandHandler(IIdentityService identityService)
    : IRequestHandler<LoginCommand, Result<AuthResultDto>>
{
    public Task<Result<AuthResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
        => identityService.LoginAsync(request.Email, request.Password, request.IpAddress, cancellationToken);
}
