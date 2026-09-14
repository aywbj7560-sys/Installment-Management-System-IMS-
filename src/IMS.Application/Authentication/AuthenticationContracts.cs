namespace IMS.Application.Authentication;

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed record CurrentUserResponse(long Id, string Email, string Role);
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserResponse User);

public interface IAuthenticationService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string FinancialManager = "Financial Manager";
    public const string SalesAgent = "Sales Agent";
    public const string CollectionOfficer = "Collection Officer";
    public const string Auditor = "Auditor";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
        { Admin, FinancialManager, SalesAgent, CollectionOfficer, Auditor });
}
