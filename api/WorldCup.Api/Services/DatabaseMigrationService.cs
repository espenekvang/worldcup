using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Models;

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
                await SeedSystemUsersAsync(dbContext, stoppingToken);
                _logger.LogInformation("Database migration completed successfully on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Database migration failed on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s...",
                    attempt, maxRetries, attempt * 2);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken);
            }
        }

        // Final attempt — let it throw if it fails
        using var finalScope = _scopeFactory.CreateScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await finalDbContext.Database.MigrateAsync(stoppingToken);
        await SeedSystemUsersAsync(finalDbContext, stoppingToken);
        _logger.LogInformation("Database migration completed successfully on final attempt");
    }

    private async Task SeedSystemUsersAsync(AppDbContext dbContext, CancellationToken ct)
    {
        var existing = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == SystemUsers.ResultServiceUserId, ct);

        if (existing is null)
        {
            dbContext.Users.Add(new User
            {
                Id = SystemUsers.ResultServiceUserId,
                GoogleId = SystemUsers.ResultServiceGoogleId,
                Email = SystemUsers.ResultServiceEmail,
                Name = SystemUsers.ResultServiceName,
                Picture = null,
                IsAdmin = false,
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded Resultatservice system user.");
        }
        else
        {
            var changed = false;
            if (!existing.IsSystem)
            {
                existing.IsSystem = true;
                changed = true;
            }
            if (existing.Name != SystemUsers.ResultServiceName)
            {
                existing.Name = SystemUsers.ResultServiceName;
                changed = true;
            }
            if (existing.Email != SystemUsers.ResultServiceEmail)
            {
                existing.Email = SystemUsers.ResultServiceEmail;
                changed = true;
            }
            if (changed)
            {
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Updated Dommeren system user metadata.");
            }
        }
    }
}
