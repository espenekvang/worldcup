using Microsoft.Data.Sqlite;

namespace WorldCup.Api.Services;

public class DatabaseBackupService : BackgroundService
{
    private static readonly TimeSpan BackupInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private const int MaxBackups = 7;
    private const string MountPath = "/mnt/backup";
    private const string DatabasePath = $"{MountPath}/worldcup.db";
    private const string BackupDirectory = $"{MountPath}/backups";

    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(ILogger<DatabaseBackupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup and file share mount to complete
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!Directory.Exists(MountPath))
                {
                    _logger.LogWarning("Mount path {Path} does not exist, skipping backup cycle", MountPath);
                }
                else
                {
                    PerformBackup();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed");
            }

            await Task.Delay(BackupInterval, stoppingToken);
        }
    }

    private void PerformBackup()
    {
        if (!File.Exists(DatabasePath))
        {
            _logger.LogWarning("Database file not found at {Path}, skipping backup", DatabasePath);
            return;
        }

        Directory.CreateDirectory(BackupDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm");
        var backupPath = Path.Combine(BackupDirectory, $"worldcup_{timestamp}.db");

        using (var source = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadOnly"))
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
}
