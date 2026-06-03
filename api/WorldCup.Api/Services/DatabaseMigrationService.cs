using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services;

/// <summary>
/// Runs EF Core migrations at startup and blocks the host from accepting HTTP requests
/// until the database is ready. This prevents race conditions where API calls arrive
/// before the schema is up to date.
/// </summary>
public class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 5;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);
                await SeedSystemUsersAsync(dbContext, cancellationToken);
                logger.LogInformation("Database migration completed successfully on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(
                    ex,
                    "Database migration failed on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s...",
                    attempt, maxRetries, attempt * 2);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }

        // Final attempt — let it throw and abort startup if it fails
        using var finalScope = scopeFactory.CreateScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await finalDbContext.Database.MigrateAsync(cancellationToken);
        await SeedSystemUsersAsync(finalDbContext, cancellationToken);
        logger.LogInformation("Database migration completed successfully on final attempt");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
            logger.LogInformation("Seeded Resultatservice system user.");
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
                logger.LogInformation("Updated Dommeren system user metadata.");
            }
        }
    }
}
