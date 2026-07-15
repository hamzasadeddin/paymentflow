namespace PaymentFlow.Application.Features.Admin;

/// <summary>A user row for the administration screen.</summary>
public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc);

// ---------- API request bodies ----------

public record CreateUserRequest(string Email, string DisplayName, string Password, IReadOnlyList<string> Roles);

public record SetUserRolesRequest(IReadOnlyList<string> Roles);

public record ResetUserPasswordRequest(string NewPassword);
