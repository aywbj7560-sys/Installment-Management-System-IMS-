using System.Net.Mail;
using System.Text.Json.Serialization;
using IMS.Application.Authentication;

namespace IMS.Application.Users;

public sealed record UserResponse(long UserId, string Username, string Email, string FullName,
    string Role, bool IsActive, DateTime CreatedAt);
public sealed record UserPage(IReadOnlyList<UserResponse> Items, int TotalCount, int Page, int PageSize);
public sealed record RoleResponse(long RoleId, string RoleName, string? Description);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateUserRequest
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;

    public Dictionary<string, string[]> Validate()
    {
        var errors = UserValidation.Profile(Username, Email, FullName, Role);
        if (string.IsNullOrEmpty(Password) || Password.Length is < 16 or > 1024)
            errors["password"] = ["Password must be 16-1024 characters."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateUserRequest
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }

    public Dictionary<string, string[]> Validate() => UserValidation.Profile(Username, Email, FullName, Role);
}

internal static class UserValidation
{
    public static Dictionary<string, string[]> Profile(string username, string email, string fullName, string role)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 50 || username.Contains('\0'))
            errors["username"] = ["Username is required and must be at most 50 characters without null characters."];
        var normalizedEmail = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || normalizedEmail.Length > 100 || email.Contains('\0')
            || !MailAddress.TryCreate(normalizedEmail, out var parsed) || parsed.Address != normalizedEmail)
            errors["email"] = ["A valid email of at most 100 characters is required."];
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100 || fullName.Contains('\0'))
            errors["fullName"] = ["Full name is required and must be at most 100 characters without null characters."];
        if (!RoleNames.All.Contains(role))
            errors["role"] = ["Use one of the existing system roles."];
        return errors;
    }
}

public enum UserWriteError
{
    None, NotFound, DuplicateUsername, DuplicateEmail, InvalidRole, SelfAdministration,
    LastActiveAdmin
}

public sealed record UserWriteResult(UserResponse? User, UserWriteError Error = UserWriteError.None);

public interface IUserAdministrationService
{
    Task<UserPage> ListAsync(string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
    Task<UserResponse?> GetAsync(long id, CancellationToken cancellationToken);
    Task<UserWriteResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserWriteResult> UpdateAsync(long id, long currentUserId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken);
}
