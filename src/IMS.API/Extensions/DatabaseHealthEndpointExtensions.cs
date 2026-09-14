using IMS.Infrastructure.Persistence;

namespace IMS.API.Extensions;

public static class DatabaseHealthEndpointExtensions
{
    public static WebApplication MapDatabaseHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/health/database", async (ImsDbContext context, CancellationToken cancellationToken) =>
        {
            var connected = await DatabaseConnectivityVerifier.CanReadMappedTablesAsync(context, cancellationToken);
            return Results.Json(
                new { database = connected ? "connected" : "unavailable" },
                statusCode: connected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
