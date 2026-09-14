using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Tests;

public class AuthenticationTests
{
    [Theory]
    [InlineData("{email:broken}", "application/json", 400)]
    [InlineData("{\"email\":", "application/json", 400)]
    [InlineData("null", "application/json", 400)]
    [InlineData("", "application/json", 400)]
    [InlineData("{}", "text/plain", 415)]
    public async Task InvalidLoginBodiesReturnClientErrorsInDevelopment(string body, string contentType, int status)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        var settings = Settings();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = settings.Key, ["Jwt:Issuer"] = settings.Issuer,
            ["Jwt:Audience"] = settings.Audience
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        await using var app = builder.Build();
        app.UseExceptionHandler(handler => handler.Run(context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        }));
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.MapAuthenticationEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PostAsync("/api/auth/login", new StringContent(body, System.Text.Encoding.UTF8, contentType));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.DoesNotContain("Exception", await response.Content.ReadAsStringAsync());
    }

    private static JwtSettings Settings() => new() { Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
        Issuer = "test-issuer", Audience = "test-audience", ExpirationMinutes = 30 };

    [Fact]
    public void PasswordHashIsSaltedAndRejectsWrongPassword()
    {
        var hasher = new PasswordHasher<User>();
        var user = new User();
        var hash = hasher.HashPassword(user, "test-password");
        Assert.NotEqual("test-password", hash);
        Assert.NotEqual(hash, hasher.HashPassword(user, "test-password"));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, hash, "test-password"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, "incorrect"));
    }

    [Theory]
    [InlineData("correct", true, true)]
    [InlineData("incorrect", true, false)]
    [InlineData("correct", false, false)]
    public async Task LoginChecksPasswordAndActiveStatus(string password, bool active, bool expected)
    {
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hasher = new PasswordHasher<User>();
        var user = new User { UserId = 1, Email = "admin@example.com", IsActive = active,
            Role = new Role { RoleId = 1, RoleName = RoleNames.Admin } };
        user.PasswordHash = hasher.HashPassword(user, "correct");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthenticationService(db, hasher, new JwtTokenGenerator(Settings()), new DummyPasswordHash(hasher));
        var result = await service.LoginAsync(new LoginRequest { Email = user.Email, Password = password }, default);
        Assert.Equal(expected, result is not null);
        if (result is not null) Assert.Equal(RoleNames.Admin, result.User.Role);
        Assert.Null(await service.LoginAsync(new LoginRequest { Email = "missing@example.com", Password = password }, default));
        user.PasswordHash = "not-a-password-hash";
        await db.SaveChangesAsync();
        Assert.Null(await service.LoginAsync(new LoginRequest { Email = user.Email, Password = user.PasswordHash }, default));
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.FinancialManager)]
    [InlineData(RoleNames.SalesAgent)]
    [InlineData(RoleNames.CollectionOfficer)]
    [InlineData(RoleNames.Auditor)]
    public async Task JwtAndMiddlewareEnforceAuthenticationAndRoles(string role)
    {
        var settings = Settings();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = settings.Key, ["Jwt:Issuer"] = settings.Issuer,
            ["Jwt:Audience"] = settings.Audience, ["Jwt:ExpirationMinutes"] = "30"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        // Login service resolution is not needed for these middleware tests.
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.MapAuthenticationEndpoints();
        foreach (var name in RoleNames.All)
            app.MapGet("/roles/" + name.Replace(' ', '-'), () => "ok").RequireAuthorization(name);
        await app.StartAsync();
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var user = new User { UserId = 1, Email = "user@example.com", Role = new Role { RoleName = role } };
        var response = new JwtTokenGenerator(settings).Create(user);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(response.Token, settings.ValidationParameters(), out _);
        Assert.Equal("1", principal.FindFirst("sub")!.Value);
        Assert.Equal(user.Email, principal.FindFirst("email")!.Value);
        Assert.True(principal.IsInRole(role));
        Assert.DoesNotContain(handler.ReadJwtToken(response.Token).Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.Token);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.DoesNotContain("password", await me.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        foreach (var name in RoleNames.All)
            Assert.Equal(name == role ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                (await client.GetAsync("/roles/" + name.Replace(' ', '-'))).StatusCode);
        Assert.Equal(role == RoleNames.Admin ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/auth/admin-check")).StatusCode);
        var wrongSettings = Settings();
        foreach (var invalid in new[] { "invalid-token", new JwtTokenGenerator(wrongSettings).Create(user).Token,
            new JwtTokenGenerator(new JwtSettings { Key = settings.Key, Issuer = "wrong", Audience = settings.Audience }).Create(user).Token,
            new JwtTokenGenerator(new JwtSettings { Key = settings.Key, Issuer = settings.Issuer, Audience = "wrong" }).Create(user).Token,
            handler.WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience, notBefore: DateTime.UtcNow.AddMinutes(-2), expires: DateTime.UtcNow.AddMinutes(-1), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(settings.Key)), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256))) })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }
    }

    [Fact]
    public void InvalidJwtConfigurationFailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtSettings().Validate());
        var settings = Settings(); settings.ExpirationMinutes = 0;
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }
}

