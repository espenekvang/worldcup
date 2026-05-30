using Microsoft.Data.Sqlite;

namespace WorldCup.Api.Services;

public class DatabaseBackupService : BackgroundService
{
    private static readonly TimeSpan BackupInterval = TimeSpan.FromHours(6);
    private const int MaxBackups = 7;
    private const string DatabasePath = "/mnt/backup/worldcup.db";
    private const string BackupDirectory = "/mnt/backup/backups";

    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(ILogger<DatabaseBackupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup to complete before first backup
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformBackupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed");
            }

            await Task.Delay(BackupInterval, stoppingToken);
        }
    }

    private Task PerformBackupAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(DatabasePath))
        {
            _logger.LogWarning("Database file not found at {Path}, skipping backup", DatabasePath);
            return Task.CompletedTask;
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

        return Task.CompletedTask;
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
