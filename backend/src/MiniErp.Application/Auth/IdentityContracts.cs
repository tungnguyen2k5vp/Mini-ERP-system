using System.ComponentModel.DataAnnotations;

namespace MiniErp.Application.Auth;

/// <summary>Credentials for an existing employee account.</summary>
public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(128)] string Password);

/// <summary>Requests a password change for the authenticated employee.</summary>
public sealed record ChangePasswordRequest(
    [property: Required, StringLength(128)] string CurrentPassword,
    [property: Required, StringLength(128, MinimumLength = 12)] string NewPassword);

/// <summary>Profile and current effective permissions.</summary>
public sealed record UserProfile(Guid Id, string Email, string DisplayName, string[] Roles, string[] Permissions);

public sealed record IssuedSession(Guid SessionId, string RefreshToken, DateTimeOffset RefreshExpiresAt, UserProfile User);

/// <summary>Provisions a new account with a single initial role.</summary>
public sealed record CreateUserRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(100)] string DisplayName,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password,
    [property: Required, StringLength(100)] string Role);

/// <summary>Changes a user's role or administrative lock.</summary>
public sealed record UpdateUserRequest(
    [property: Required, StringLength(100)] string Role,
    bool IsDisabled,
    [property: Required, StringLength(80)] string Version);

/// <summary>Account details available to administrators.</summary>
public sealed record UserAccount(Guid Id, string Email, string DisplayName, string Role, bool IsDisabled,
    bool IsLockedOut, string Version);

/// <summary>Effective permission grants for a built-in role.</summary>
public sealed record RoleAccess(string Name, string[] Permissions, string Version);

/// <summary>Replaces configurable permission grants for a role.</summary>
public sealed record UpdateRoleRequest(
    [property: Required, MaxLength(50)] string[] Permissions,
    [property: Required, StringLength(80)] string Version);

public interface IIdentityService
{
    Task<IssuedSession?> LoginAsync(LoginRequest request, RequestActor actor, CancellationToken ct);
    Task<IssuedSession?> RefreshAsync(string rawToken, CancellationToken ct);
    Task LogoutAsync(string? rawToken, Guid? sessionId, RequestActor actor, CancellationToken ct);
    Task<UserProfile?> GetProfileAsync(Guid id, CancellationToken ct);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, RequestActor actor, CancellationToken ct);
    Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken ct);
}

public interface IIdentityAdministration
{
    Task<Page<UserAccount>> ListUsersAsync(string? search, int page, int size, CancellationToken ct);
    Task<UserAccount> CreateUserAsync(CreateUserRequest request, RequestActor actor, CancellationToken ct);
    Task<UserAccount> UpdateUserAsync(Guid id, UpdateUserRequest request, RequestActor actor, CancellationToken ct);
    Task<RoleAccess[]> ListRolesAsync(CancellationToken ct);
    Task<RoleAccess> UpdateRoleAsync(string name, UpdateRoleRequest request, RequestActor actor, CancellationToken ct);
}
