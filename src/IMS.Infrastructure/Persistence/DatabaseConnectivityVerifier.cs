using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IMS.Infrastructure.Persistence;

public static class DatabaseConnectivityVerifier
{
    public static async Task<bool> CanReadMappedTablesAsync(
        ImsDbContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
                return false;

            var entities = context.Model.GetEntityTypes().ToArray();
            if (entities.Length != 12)
                return false;

            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.Database != "ims_db")
                    return false;

                foreach (var entity in entities)
                {
                    var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                    var columns = entity.GetProperties()
                        .Select(property => Quote(property.GetColumnName(table)!));
                    await using var command = connection.CreateCommand();
                    command.CommandTimeout = 5;
                    // Resolve every mapped column and check SELECT permission without reading rows.
                    command.CommandText =
                        $"SELECT {string.Join(", ", columns)} FROM {Quote(table.Schema!)}.{Quote(table.Name)} WHERE FALSE";
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                }

                return true;
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
        catch (DbException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
