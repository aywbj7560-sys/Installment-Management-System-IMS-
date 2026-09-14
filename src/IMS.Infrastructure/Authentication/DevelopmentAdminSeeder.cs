using System.Net.Mail;
using IMS.Application.Authentication;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IMS.Infrastructure.Authentication;

public sealed class DevelopmentAdminSeeder(ImsDbContext db, IPasswordHasher<User> hasher,
    IConfiguration configuration, IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return;
        var email = configuration["SeedAdmin:Email"]?.Trim();
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password)) return;
        if (string.IsNullOrWhiteSpace(email) || email.Length > 100 || !MailAddress.TryCreate(email, out var parsed)
            || parsed.Address != email || string.IsNullOrEmpty(password) || password.Length is < 16 or > 1024
            || password.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Development admin seed requires a valid email and a password of 16-1024 characters.");

        // Serialize seed attempts across development instances without schema changes.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730184221)", cancellationToken);
        if (!await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            var role = await db.Roles.SingleAsync(x => x.RoleName == RoleNames.Admin, cancellationToken);
            var user = new User { Email = email, Username = "dev-admin-" + Guid.NewGuid().ToString("N"),
                FullName = "Development Administrator", RoleId = role.RoleId, IsActive = true };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
