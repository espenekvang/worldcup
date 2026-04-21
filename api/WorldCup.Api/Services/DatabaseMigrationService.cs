using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;

namespace WorldCup.Api.Services;

public class DatabaseMigrationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(IServiceScopeFactory scopeFactory, ILogger<DatabaseMigrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const int maxRetries = 5;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync(stoppingToken);
                _logger.LogInformation("Database migration completed successfully on attempt {Attempt}", attempt);
                return;
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 15 && attempt < maxRetries)
            {
                _logger.LogWarning(
                    "SQLite locking protocol error on migration attempt {Attempt}/{MaxRetries}, retrying in {Delay}s...",
                    attempt, maxRetries, attempt * 2);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 15)
            {
                // Azure Files (SMB) does not support SQLite locking. When another revision
                // holds the DB file open, ALL SQLite operations fail — including the
                // read-only check for pending migrations. Since the healthy revision is
                // already running with the current schema, it is safe to skip migration
                // and let the app start. If there actually were unapplied migrations the
                // app would fail on first DB access, which is an acceptable signal.
                _logger.LogWarning(
                    ex,
                    "SQLite locking protocol error persisted after {MaxRetries} attempts. "
                    + "Skipping migration — assuming another revision already applied all migrations.",
                    maxRetries);
            }
        }
    }
}
