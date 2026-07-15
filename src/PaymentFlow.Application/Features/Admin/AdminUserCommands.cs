using FluentValidation;
using MediatR;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Constants;

namespace PaymentFlow.Application.Features.Admin;

// ---------- Create user ----------

public record CreateUserCommand(string Email, string DisplayName, string Password, IReadOnlyList<string> Roles)
    : IRequest<Result<AdminUserDto>>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10);
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles)
            .Must(Roles.All.Contains)
            .WithMessage("Unknown role.");
    }
}

public sealed class CreateUserCommandHandler(IUserAdminService userAdmin, ICurrentUserService currentUser)
    : IRequestHandler<CreateUserCommand, Result<AdminUserDto>>
{
    public Task<Result<AdminUserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        => userAdmin.CreateAsync(request.Email, request.DisplayName, request.Password, request.Roles,
            currentUser.UserId?.ToString(), currentUser.Email, cancellationToken);
}

// ---------- Activate / deactivate ----------

public record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest<Result<AdminUserDto>>;

public sealed class SetUserActiveCommandHandler(IUserAdminService userAdmin, ICurrentUserService currentUser)
    : IRequestHandler<SetUserActiveCommand, Result<AdminUserDto>>
{
    public Task<Result<AdminUserDto>> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
        => userAdmin.SetActiveAsync(request.UserId, request.IsActive,
            currentUser.UserId?.ToString(), currentUser.Email, cancellationToken);
}

// ---------- Set roles ----------

public record SetUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles) : IRequest<Result<AdminUserDto>>;

public sealed class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesCommandValidator()
    {
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles)
            .Must(Roles.All.Contains)
            .WithMessage("Unknown role.");
    }
}

public sealed class SetUserRolesCommandHandler(IUserAdminService userAdmin, ICurrentUserService currentUser)
    : IRequestHandler<SetUserRolesCommand, Result<AdminUserDto>>
{
    public Task<Result<AdminUserDto>> Handle(SetUserRolesCommand request, CancellationToken cancellationToken)
        => userAdmin.SetRolesAsync(request.UserId, request.Roles,
            currentUser.UserId?.ToString(), currentUser.Email, cancellationToken);
}

// ---------- Reset password ----------

public record ResetUserPasswordCommand(Guid UserId, string NewPassword) : IRequest<Result>;

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
        => RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10);
}

public sealed class ResetUserPasswordCommandHandler(IUserAdminService userAdmin, ICurrentUserService currentUser)
    : IRequestHandler<ResetUserPasswordCommand, Result>
{
    public Task<Result> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
        => userAdmin.ResetPasswordAsync(request.UserId, request.NewPassword,
            currentUser.UserId?.ToString(), currentUser.Email, cancellationToken);
}
