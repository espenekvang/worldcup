using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorldCup.Api.Data;

/// <summary>
/// Sets SQLite PRAGMAs on every new database connection to ensure reliable writes
/// on Azure Files (SMB). WAL mode is incompatible with SMB-mounted file systems
/// because SMB does not support the shared-memory file (-shm) required for WAL readers.
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
        cmd.CommandText = "PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;";
        cmd.ExecuteNonQuery();
    }
}
