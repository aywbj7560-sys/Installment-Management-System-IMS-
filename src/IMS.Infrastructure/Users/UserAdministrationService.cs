using IMS.Application.Authentication;
using IMS.Application.Users;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IMS.Infrastructure.Users;

public sealed class UserAdministrationService(ImsDbContext db, IPasswordHasher<User> hasher)
    : IUserAdministrationService
{
    public async Task<UserPage> ListAsync(string? search, string? role, bool? isActive, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (role is not null) query = query.Where(x => x.Role.RoleName == role);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FullName.ToLower().Contains(term) || x.Email.ToLower().Contains(term));
        }
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.UserId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new UserResponse(x.UserId, x.Username, x.Email, x.FullName, x.Role.RoleName,
                x.IsActive, x.CreatedAt)).ToArrayAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }

    public Task<UserResponse?> GetAsync(long id, CancellationToken cancellationToken) => db.Users.AsNoTracking()
        .Where(x => x.UserId == id)
        .Select(x => new UserResponse(x.UserId, x.Username, x.Email, x.FullName, x.Role.RoleName,
            x.IsActive, x.CreatedAt)).SingleOrDefaultAsync(cancellationToken);

    public async Task<UserWriteResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var role = await ExistingRole(request.Role, cancellationToken);
        if (role is null) return new(null, UserWriteError.InvalidRole);
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.Username == username, cancellationToken))
            return new(null, UserWriteError.DuplicateUsername);
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return new(null, UserWriteError.DuplicateEmail);
        var user = new User { Username = username, Email = email, FullName = request.FullName.Trim(),
            RoleId = role.RoleId, Role = role, IsActive = request.IsActive };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (UniqueConstraint(ex) is { } error)
        {
            db.Entry(user).State = EntityState.Detached;
            return new(null, error);
        }
        return new(Response(user));
    }

    public async Task<UserWriteResult> UpdateAsync(long id, long currentUserId, UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsNpgsql()
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (transaction is not null)
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730184222)", cancellationToken);
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (user is null) return new(null, UserWriteError.NotFound);
        var role = await ExistingRole(request.Role, cancellationToken);
        if (role is null) return new(null, UserWriteError.InvalidRole);
        var removesAdmin = user.Role.RoleName == RoleNames.Admin && (role.RoleName != RoleNames.Admin || !request.IsActive);
        if (id == currentUserId && (!request.IsActive || role.RoleName != RoleNames.Admin))
            return new(null, UserWriteError.SelfAdministration);
        if (removesAdmin && await db.Users.CountAsync(x => x.IsActive && x.Role.RoleName == RoleNames.Admin, cancellationToken) <= 1)
            return new(null, UserWriteError.LastActiveAdmin);
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.Username == username && x.UserId != id, cancellationToken))
            return new(null, UserWriteError.DuplicateUsername);
        if (await db.Users.AnyAsync(x => x.Email == email && x.UserId != id, cancellationToken))
            return new(null, UserWriteError.DuplicateEmail);
        user.Username = username; user.Email = email; user.FullName = request.FullName.Trim();
        user.RoleId = role.RoleId; user.Role = role; user.IsActive = request.IsActive;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (UniqueConstraint(ex) is { } error)
        {
            db.Entry(user).State = EntityState.Unchanged;
            return new(null, error);
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(Response(user));
    }

    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().Where(x => RoleNames.All.Contains(x.RoleName))
            .ToDictionaryAsync(x => x.RoleName, cancellationToken);
        return RoleNames.All.Where(roles.ContainsKey).Select(name => roles[name])
            .Select(x => new RoleResponse(x.RoleId, x.RoleName, x.Description)).ToArray();
    }

    private Task<Role?> ExistingRole(string name, CancellationToken cancellationToken) =>
        !RoleNames.All.Contains(name) ? Task.FromResult<Role?>(null) :
            db.Roles.SingleOrDefaultAsync(x => x.RoleName == name, cancellationToken);

    private static UserWriteError? UniqueConstraint(DbUpdateException exception) => exception.InnerException switch
    {
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "users_username_key" }
            => UserWriteError.DuplicateUsername,
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "users_email_key" }
            => UserWriteError.DuplicateEmail,
        _ => null
    };

    private static UserResponse Response(User x) => new(x.UserId, x.Username, x.Email, x.FullName,
        x.Role.RoleName, x.IsActive, x.CreatedAt);
}
