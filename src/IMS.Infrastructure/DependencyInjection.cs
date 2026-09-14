using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:DefaultConnection using environment configuration.");
        }

        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Do not include the supplied value or provider exception in diagnostics.
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not a valid PostgreSQL connection string.");
        }

        if (string.IsNullOrWhiteSpace(settings.Host) || settings.Database != "ims_db")
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must specify a host and Database=ims_db.");
        }

        settings.IncludeErrorDetail = false;
        settings.PersistSecurityInfo = false;
        services.AddDbContext<ImsDbContext>(options =>
            options.UseNpgsql(settings.ConnectionString));
        return services;
    }
}
