using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services;

public sealed class ResultFetcherService(
    IServiceScopeFactory scopeFactory,
    MatchSchedule schedule,
    Wc2026ApiClient apiClient,
    ILogger<ResultFetcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MatchCompletionBuffer = TimeSpan.FromHours(2.5);
    private static readonly DateTime TournamentCutoffUtc = new(2026, 7, 20, 23, 59, 59, DateTimeKind.Utc);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow > TournamentCutoffUtc)
                {
                    logger.LogInformation("Tournament cutoff reached. Stopping result polling service.");
                    break;
                }

                await CheckForCompletedMatchesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while fetching results.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CheckForCompletedMatchesAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Checking for completed matches");

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scoringService = scope.ServiceProvider.GetRequiredService<ScoringService>();

        var existingMatchIds = await dbContext.MatchResults
            .Select(result => result.MatchId)
            .ToListAsync(stoppingToken);

        var existingMatchIdSet = existingMatchIds.ToHashSet();
        var now = DateTime.UtcNow;
        var hasDueMatches = schedule.GetAllMatches()
            .Any(match => match.Date + MatchCompletionBuffer < now && !existingMatchIdSet.Contains(match.Id));

        if (!hasDueMatches)
        {
            logger.LogInformation("Skipping poll — no matches expected");
            return;
        }

        var completedMatches = await apiClient.GetCompletedMatchesAsync(stoppingToken);
        var newResults = 0;

        foreach (var dto in completedMatches)
        {
            if (dto.Score?.Ft is not [var homeScore, var awayScore])
            {
                continue;
            }

            var matchId = apiClient.MapToLocalMatchId(dto.KickoffAt, schedule);
            if (matchId is null || existingMatchIdSet.Contains(matchId.Value))
            {
                continue;
            }

            dbContext.MatchResults.Add(new MatchResult
            {
                Id = Guid.NewGuid(),
                MatchId = matchId.Value,
                HomeScore = homeScore,
                AwayScore = awayScore,
                FetchedAt = DateTime.UtcNow
            });

            var predictions = await dbContext.Predictions
                .Where(prediction => prediction.MatchId == matchId.Value)
                .ToListAsync(stoppingToken);

            foreach (var prediction in predictions)
            {
                prediction.Points = scoringService.CalculatePoints(
                    prediction.HomeScore,
                    prediction.AwayScore,
                    homeScore,
                    awayScore);
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            existingMatchIdSet.Add(matchId.Value);
            newResults++;
            logger.LogInformation("Calculated points for match {MatchId}", matchId.Value);
        }

        logger.LogInformation("Found {Count} new results", newResults);
    }
}
