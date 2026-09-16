using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Users;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Persistence;
using IMS.Infrastructure.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Tests;

public class UserAdministrationTests
{
    private const string ValidPassword = "a-secure-test-password";

    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:Key"] = "users-test-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests" });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        app.MapAuthenticationEndpoints(); app.MapUserEndpoints();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var roles = RoleNames.All.Select((name, index) => new Role { RoleId = index + 1, RoleName = name }).ToArray();
            db.Roles.AddRange(roles);
            var admin = new User { UserId = 1, Role = roles[0], RoleId = roles[0].RoleId, Username = "admin",
                Email = "admin@example.com", FullName = "Admin User", IsActive = true, CreatedAt = DateTime.UtcNow };
            admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, ValidPassword);
            db.Users.Add(admin); await db.SaveChangesAsync();
        }
        await app.StartAsync(); return app;
    }

    private static HttpClient Client(WebApplication app, string? role, long id = 1)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User
            { UserId = id, Email = "actor@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }

    private static CreateUserRequest Create(string suffix = "one", string role = RoleNames.SalesAgent) => new()
    { Username = "user-" + suffix, Email = $"{suffix}@example.com", FullName = "Test " + suffix, Password = ValidPassword, Role = role, IsActive = true };
    private static UpdateUserRequest Update(UserResponse user, string? role = null, bool? active = null, string? email = null) => new()
    { Username = user.Username, Email = email ?? user.Email, FullName = user.FullName, Role = role ?? user.Role, IsActive = active ?? user.IsActive };

    [Theory]
    [InlineData(null, 401)]
    [InlineData(RoleNames.Admin, 200)]
    [InlineData(RoleNames.FinancialManager, 403)]
    [InlineData(RoleNames.SalesAgent, 403)]
    [InlineData(RoleNames.CollectionOfficer, 403)]
    [InlineData(RoleNames.Auditor, 403)]
    [InlineData("Unknown", 403)]
    public async Task EveryRouteIsAdminOnly(string? role, int expected)
    {
        await using var app = await Host(); using var client = Client(app, role);
        Assert.Equal(expected, (int)(await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(expected, (int)(await client.GetAsync("/api/users/1")).StatusCode);
        Assert.Equal(expected == 200 ? 201 : expected, (int)(await client.PostAsJsonAsync("/api/users", Create(Guid.NewGuid().ToString("N")))).StatusCode);
        Assert.Equal(expected, (int)(await client.PutAsJsonAsync("/api/users/1", new UpdateUserRequest
        { Username = "admin", Email = "admin@example.com", FullName = "Admin User", Role = RoleNames.Admin, IsActive = true })).StatusCode);
        Assert.Equal(expected, (int)(await client.GetAsync("/api/roles")).StatusCode);
    }

    [Fact]
    public async Task CreateReadFilterAndUpdateNeverExposePassword()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var response = await client.PostAsJsonAsync("/api/users", Create());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ValidPassword, body);
        var user = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
        var page = (await client.GetFromJsonAsync<UserPage>("/api/users?search=ONE&role=Sales%20Agent&isActive=true&pageSize=1"))!;
        Assert.Single(page.Items); Assert.Equal(user.UserId, page.Items[0].UserId);
        Assert.Empty((await client.GetFromJsonAsync<UserPage>("/api/users?search=missing"))!.Items);
        var updatedResponse = await client.PutAsJsonAsync(response.Headers.Location, Update(user, RoleNames.Auditor, false, "changed@example.com"));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.False(updated.IsActive); Assert.Equal(RoleNames.Auditor, updated.Role); Assert.Equal("changed@example.com", updated.Email);
        await using var scope = app.Services.CreateAsyncScope(); var saved = await scope.ServiceProvider.GetRequiredService<ImsDbContext>().Users.FindAsync(user.UserId);
        Assert.NotNull(saved); Assert.NotEqual(ValidPassword, saved.PasswordHash); Assert.NotEmpty(saved.PasswordHash);
    }

    [Fact]
    public async Task ConflictsValidationMalformedAndImmutableFieldsAreProtected()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var first = (await (await client.PostAsJsonAsync("/api/users", Create())).Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", Create())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest
        { Username = "different", Email = first.Email, FullName = "Other", Password = ValidPassword, Role = RoleNames.Auditor })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", Create("bad", "Invented"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest
        { Username = "bad", Email = "not-email", FullName = "Bad", Password = "short", Role = RoleNames.Admin })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/users", new StringContent(
            "{\"username\":null,\"email\":null,\"password\":null,\"fullName\":null,\"role\":null}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/users", new StringContent("{", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/users", new StringContent(
            "{\"username\":\"x\",\"email\":\"x@example.com\",\"password\":\"1234567890123456\",\"fullName\":\"X\",\"role\":\"Admin\",\"unknown\":1}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/users/0")).StatusCode);
        var originalCreatedAt = first.CreatedAt;
        var changed = (await (await client.PutAsJsonAsync($"/api/users/{first.UserId}", Update(first, email: "new@example.com"))).Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(originalCreatedAt, changed.CreatedAt); Assert.Equal(first.UserId, changed.UserId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{first.UserId}", Update(first, email: "admin@example.com"))).StatusCode);
    }

    [Fact]
    public async Task SelfAndLastAdminSafeguardsAndRoleImmutabilityApply()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var admin = (await client.GetFromJsonAsync<UserResponse>("/api/users/1"))!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/users/1", Update(admin, active: false))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/users/1", Update(admin, RoleNames.Auditor))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync("/api/users/1")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/api/roles", new { roleName = "New" })).StatusCode);
        Assert.Equal(5, (await client.GetFromJsonAsync<RoleResponse[]>("/api/roles"))!.Length);
    }

    [Fact]
    public async Task InactiveUserCannotLoginAndReactivationRestoresLogin()
    {
        await using var app = await Host(); using var admin = Client(app, RoleNames.Admin);
        var created = (await (await admin.PostAsJsonAsync("/api/users", Create("login"))).Content.ReadFromJsonAsync<UserResponse>())!;
        async Task<HttpStatusCode> Login() => (await app.GetTestClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = created.Email, Password = ValidPassword })).StatusCode;
        Assert.Equal(HttpStatusCode.OK, await Login());
        await admin.PutAsJsonAsync($"/api/users/{created.UserId}", Update(created, active: false));
        Assert.Equal(HttpStatusCode.Unauthorized, await Login());
        await admin.PutAsJsonAsync($"/api/users/{created.UserId}", Update(created, active: true));
        Assert.Equal(HttpStatusCode.OK, await Login());
    }
}
