using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorldCup.Api.Data;

/// <summary>
/// Sets SQLite PRAGMAs on every new database connection for optimal performance.
/// The database now runs on local (ephemeral) disk, so WAL mode is safe to use.
/// </summary>
public class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        SetPragmas(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void SetPragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
    }
}
