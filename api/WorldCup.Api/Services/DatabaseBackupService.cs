using Microsoft.Data.Sqlite;

namespace WorldCup.Api.Services;

/// <summary>
/// Manages database durability by restoring from Azure Files on startup (if local DB
/// is missing) and periodically backing up the local database to the Azure Files mount.
/// Backups run at 02:00 UTC to minimize I/O contention during peak hours.
/// </summary>
public class DatabaseBackupService : BackgroundService
{
    private const int MaxBackups = 7;
    private const string LocalDatabasePath = "/data/worldcup.db";
    private const string MountPath = "/mnt/backup";
    private const string BackupDirectory = $"{MountPath}/backups";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeOnly BackupTimeUtc = new(2, 0); // 02:00 UTC

    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(ILogger<DatabaseBackupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        RestoreIfNeeded();

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextBackup();
            _logger.LogInformation("Next backup scheduled in {Hours:F1} hours", delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                PerformBackup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed");
            }
        }
    }

    private void RestoreIfNeeded()
    {
        if (File.Exists(LocalDatabasePath))
        {
            _logger.LogInformation("Local database exists at {Path}, skipping restore", LocalDatabasePath);
            return;
        }

        if (!Directory.Exists(BackupDirectory))
        {
            _logger.LogInformation("No backup directory found, starting with fresh database");
            return;
        }

        var latestBackup = Directory.GetFiles(BackupDirectory, "worldcup_*.db")
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (latestBackup is null)
        {
            _logger.LogInformation("No backup files found, starting with fresh database");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LocalDatabasePath)!);

        _logger.LogInformation("Restoring database from {Backup} to {Local}", latestBackup, LocalDatabasePath);

        using var source = new SqliteConnection($"Data Source={latestBackup};Mode=ReadOnly");
        using var destination = new SqliteConnection($"Data Source={LocalDatabasePath}");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);

        _logger.LogInformation("Database restore completed successfully");
    }

    private void PerformBackup()
    {
        if (!File.Exists(LocalDatabasePath))
        {
            _logger.LogWarning("Local database not found at {Path}, skipping backup", LocalDatabasePath);
            return;
        }

        if (!Directory.Exists(MountPath))
        {
            _logger.LogWarning("Mount path {Path} does not exist, skipping backup", MountPath);
            return;
        }

        Directory.CreateDirectory(BackupDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm");
        var backupPath = Path.Combine(BackupDirectory, $"worldcup_{timestamp}.db");

        using (var source = new SqliteConnection($"Data Source={LocalDatabasePath};Mode=ReadOnly"))
        using (var destination = new SqliteConnection($"Data Source={backupPath}"))
        {
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        _logger.LogInformation("Database backup created: {Path}", backupPath);

        CleanupOldBackups();
    }

    private void CleanupOldBackups()
    {
        var backupFiles = Directory.GetFiles(BackupDirectory, "worldcup_*.db")
            .OrderByDescending(f => f)
            .ToList();

        foreach (var file in backupFiles.Skip(MaxBackups))
        {
            try
            {
                File.Delete(file);
                _logger.LogInformation("Deleted old backup: {Path}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup: {Path}", file);
            }
        }
    }

    private static TimeSpan GetDelayUntilNextBackup()
    {
        var now = DateTime.UtcNow;
        var todayBackup = now.Date.Add(BackupTimeUtc.ToTimeSpan());

        var nextBackup = now < todayBackup ? todayBackup : todayBackup.AddDays(1);
        return nextBackup - now;
    }
}
