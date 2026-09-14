using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using IMS.Application.Authentication;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace IMS.API.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddImsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Development defaults to throwing for malformed JSON. Return the proper
        // 400/415 response instead of routing client errors through the 500 handler.
        services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options =>
            options.ThrowOnBadRequest = false);
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new();
        settings.Validate();
        services.AddSingleton(settings);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<DummyPasswordHash>();
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<DevelopmentAdminSeeder>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = settings.ValidationParameters();
        });
        services.AddAuthorization(options =>
        {
            foreach (var role in RoleNames.All)
                options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, IAuthenticationService authentication,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var response = await authentication.LoginAsync(request, cancellationToken);
            return response is null ? Results.Json(new { message = "Invalid email or password." }, statusCode: 401)
                : Results.Ok(response);
        }).AllowAnonymous().RequireRateLimiting("login");

        app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(CurrentUser(user))).RequireAuthorization();
        app.MapGet("/api/auth/admin-check", () => Results.Ok(new { authorized = true }))
            .RequireAuthorization(RoleNames.Admin);
    }

    private static CurrentUserResponse CurrentUser(ClaimsPrincipal user) => new(
        long.Parse(user.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        user.FindFirstValue("email")!, user.FindFirstValue("role")!);
}
