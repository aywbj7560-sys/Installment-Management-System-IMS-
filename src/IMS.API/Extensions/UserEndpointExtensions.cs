using System.Globalization;
using System.Security.Claims;
using IMS.Application.Authentication;
using IMS.Application.Users;

namespace IMS.API.Extensions;

public static class UserEndpointExtensions
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users").RequireAuthorization(RoleNames.Admin);
        users.MapGet("", async (IUserAdministrationService service, CancellationToken ct, string? search = null,
            string? role = null, bool? isActive = null, int page = 1, int pageSize = 50) =>
        {
            if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize
                || search?.Length > 150 || search?.Contains('\0') == true)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] =
                    ["Use a positive page, pageSize 1-100, and search up to 150 characters without null characters."] });
            if (role is not null && !RoleNames.All.Contains(role))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Use one of the existing system roles."] });
            return Results.Ok(await service.ListAsync(search, role, isActive, page, pageSize, ct));
        });
        users.MapGet("/{id:long}", async (long id, IUserAdministrationService service, CancellationToken ct) =>
            id <= 0 ? Results.ValidationProblem(new Dictionary<string, string[]> { ["id"] = ["Use a positive user ID."] }) :
            await service.GetAsync(id, ct) is { } user ? Results.Ok(user) : Results.NotFound());
        users.MapPost("", async (CreateUserRequest request, IUserAdministrationService service, CancellationToken ct) =>
            await Save(request.Validate(), () => service.CreateAsync(request, ct), null));
        users.MapPut("/{id:long}", async (long id, UpdateUserRequest request, ClaimsPrincipal principal,
            IUserAdministrationService service, CancellationToken ct) =>
            id <= 0 ? Results.ValidationProblem(new Dictionary<string, string[]> { ["id"] = ["Use a positive user ID."] }) :
            await Save(request.Validate(), () => service.UpdateAsync(id,
                long.Parse(principal.FindFirstValue("sub")!, CultureInfo.InvariantCulture), request, ct), id));

        app.MapGet("/api/roles", async (IUserAdministrationService service, CancellationToken ct) =>
            Results.Ok(await service.ListRolesAsync(ct))).RequireAuthorization(RoleNames.Admin);
    }

    private static async Task<IResult> Save(Dictionary<string, string[]> errors,
        Func<Task<UserWriteResult>> operation, long? id)
    {
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await operation();
        return result.Error switch
        {
            UserWriteError.NotFound => Results.NotFound(),
            UserWriteError.DuplicateUsername => Results.Conflict(new { message = "A user with this username already exists." }),
            UserWriteError.DuplicateEmail => Results.Conflict(new { message = "A user with this email already exists." }),
            UserWriteError.InvalidRole => Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["The selected system role does not exist."] }),
            UserWriteError.SelfAdministration => Results.Conflict(new { message = "An administrator cannot deactivate or remove their own Admin role." }),
            UserWriteError.LastActiveAdmin => Results.Conflict(new { message = "The last active administrator cannot be deactivated or demoted." }),
            _ => id.HasValue ? Results.Ok(result.User) : Results.Created($"/api/users/{result.User!.UserId}", result.User)
        };
    }
}
