using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services;

public sealed class ResultFetcherService(
    IServiceScopeFactory scopeFactory,
    MatchScheduleProvider scheduleProvider,
    Wc2026ApiClient apiClient,
    TeamCodeMapper teamCodeMapper,
    MatchFileWriter matchFileWriter,
    ILogger<ResultFetcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MatchCompletionBuffer = TimeSpan.FromHours(2.5);
    private static readonly DateTime TournamentCutoffUtc = new(2026, 7, 20, 23, 59, 59, DateTimeKind.Utc);
    private static readonly StringComparer StageComparer = StringComparer.OrdinalIgnoreCase;

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
                await CheckForFixtureUpdatesAsync(stoppingToken);
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
        var hasDueMatches = scheduleProvider.Current.GetAllMatches()
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

            var matchId = apiClient.MapToLocalMatchId(dto.KickoffAt, scheduleProvider.Current);
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

    private async Task CheckForFixtureUpdatesAsync(CancellationToken ct)
    {
        var currentSchedule = scheduleProvider.Current;
        var currentMatches = currentSchedule.GetAllMatches();
        var undeterminedMatchesById = currentMatches
            .Where(match =>
                match.AreTeamsUndetermined
                && !match.ManualOverride
                && !StageComparer.Equals(match.Stage, "group"))
            .ToDictionary(match => match.Id);

        if (undeterminedMatchesById.Count == 0)
        {
            logger.LogInformation("Skipping fixture update poll — no undetermined knockout matches found");
            return;
        }

        logger.LogInformation(
            "Checking fixture updates for {Count} undetermined knockout matches",
            undeterminedMatchesById.Count);

        var scheduledMatches = await apiClient.GetScheduledMatchesAsync(ct);
        var updatesById = new Dictionary<int, MatchEntry>();

        foreach (var dto in scheduledMatches)
        {
            var matchId = apiClient.MapToLocalMatchIdByMatchNumber(dto.MatchNumber, currentSchedule);
            if (matchId is null || !undeterminedMatchesById.TryGetValue(matchId.Value, out var localMatch) || !localMatch.AreTeamsUndetermined)
            {
                continue;
            }

            var homeTeamCode = string.IsNullOrWhiteSpace(dto.Home)
                ? null
                : teamCodeMapper.GetCode(dto.Home);
            var awayTeamCode = string.IsNullOrWhiteSpace(dto.Away)
                ? null
                : teamCodeMapper.GetCode(dto.Away);

            if (homeTeamCode is null && awayTeamCode is null)
            {
                continue;
            }

            var updatedHomeTeam = homeTeamCode ?? localMatch.HomeTeam;
            var updatedAwayTeam = awayTeamCode ?? localMatch.AwayTeam;

            if (updatedHomeTeam == localMatch.HomeTeam && updatedAwayTeam == localMatch.AwayTeam)
            {
                continue;
            }

            updatesById[localMatch.Id] = new MatchEntry
            {
                Id = localMatch.Id,
                Date = localMatch.Date,
                Stage = localMatch.Stage,
                HomeTeam = updatedHomeTeam,
                AwayTeam = updatedAwayTeam,
                HomePlaceholder = localMatch.HomePlaceholder,
                AwayPlaceholder = localMatch.AwayPlaceholder,
                Group = localMatch.Group,
                VenueId = localMatch.VenueId,
                ManualOverride = localMatch.ManualOverride
            };
        }

        if (updatesById.Count == 0)
        {
            logger.LogInformation("Fixture update poll completed with 0 updated matches");
            return;
        }

        var updatedMatches = currentMatches
            .Select(match => updatesById.TryGetValue(match.Id, out var updatedMatch) ? updatedMatch : match)
            .ToList();

        await matchFileWriter.WriteAsync(updatedMatches, ct);
        logger.LogInformation("Fixture update poll completed with {Count} updated matches", updatesById.Count);
    }
}
