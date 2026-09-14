using IMS.Application.Authentication;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IMS.Infrastructure.Authentication;

// A singleton dummy hash makes unknown-user attempts perform password verification too.
public sealed class DummyPasswordHash(IPasswordHasher<User> hasher)
{
    public string Value { get; } = hasher.HashPassword(new User(), Guid.NewGuid().ToString());
}

public sealed class AuthenticationService(ImsDbContext db, IPasswordHasher<User> hasher,
    JwtTokenGenerator tokens, DummyPasswordHash dummy) : IAuthenticationService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 100
            || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            return null;

        // Exact email matching respects the existing case-sensitive unique constraint.
        var user = await db.Users.Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == request.Email.Trim(), cancellationToken);
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? dummy.Value, request.Password);
        }
        catch (FormatException)
        {
            return null; // Unsupported legacy/plain-text values are never accepted.
        }
        if (user is null || !user.IsActive || result == PasswordVerificationResult.Failed
            || !RoleNames.All.Contains(user.Role.RoleName)) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }
        return tokens.Create(user);
    }
}
