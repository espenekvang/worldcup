using Microsoft.Data.Sqlite;

namespace WorldCup.Api.Services;

public class SqliteBackupService : BackgroundService
{
    private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(5);
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SqliteBackupService> _logger;

    public SqliteBackupService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SqliteBackupService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backupDirectory = SqliteBackupPaths.ResolveBackupDirectory(_configuration);
        if (!Directory.Exists(backupDirectory))
        {
            _logger.LogInformation("Skipping SQLite backups because backup directory '{BackupDirectory}' does not exist.", backupDirectory);
            return;
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Skipping SQLite backups because the default connection string is not configured.");
            return;
        }

        var localDatabasePath = SqliteBackupPaths.ResolveLocalDatabasePath(connectionString, _environment.ContentRootPath);
        var backupDatabasePath = SqliteBackupPaths.ResolveBackupDatabasePath(_configuration);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(localDatabasePath))
                {
                    File.Copy(localDatabasePath, backupDatabasePath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to back up SQLite database from '{LocalDatabasePath}' to '{BackupDatabasePath}'.", localDatabasePath, backupDatabasePath);
            }

            await Task.Delay(BackupInterval, stoppingToken);
        }
    }
}

internal static class SqliteBackupPaths
{
    private const string DefaultBackupDirectory = "/mnt/backup";
    private const string BackupFileName = "worldcup.db";

    public static string ResolveLocalDatabasePath(string connectionString, string contentRootPath)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new InvalidOperationException("SQLite connection string must include a Data Source.");
        }

        return Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.GetFullPath(dataSource, contentRootPath);
    }

    public static string ResolveBackupDirectory(IConfiguration configuration) =>
        configuration["Backup:Path"] is { Length: > 0 } configuredPath
            ? configuredPath
            : DefaultBackupDirectory;

    public static string ResolveBackupDatabasePath(IConfiguration configuration) =>
        Path.Combine(ResolveBackupDirectory(configuration), BackupFileName);
}
