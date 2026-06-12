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
    // Stage-aware buffer: how long after kickoff we *expect* a result to be available.
    // Group games: 90' + HT + ~15 min stoppage + API publish delay.
    // Knockout: add 30' ET + ~15 min penalty buffer.
    private static readonly TimeSpan GroupStageBuffer = TimeSpan.FromHours(2.10);
    private static readonly TimeSpan KnockoutBuffer = TimeSpan.FromHours(3.25);
    private static readonly TimeSpan FinalBuffer = TimeSpan.FromHours(3.5);

    // Backoff between retry attempts when a poll returns no result for a due match.
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private const int MaxFetchAttempts = 5;

    // Safety caps. We always wake at least this often, even if no match is due,
    // so config changes / schedule reloads are picked up. And we never sleep less
    // than the minimum to avoid tight loops on edge cases.
    private static readonly TimeSpan MaxSleep = TimeSpan.FromHours(6);
    private static readonly TimeSpan MinSleep = TimeSpan.FromSeconds(30);

    // Tolerance window — if a match is "due" within this window we'll poll now
    // instead of going back to sleep for a few seconds.
    private static readonly TimeSpan DueTolerance = TimeSpan.FromMinutes(2);

    // Daily budget: upstream provider allows 100 calls / 24h. We leave headroom
    // for ad-hoc/manual calls and to absorb retries triggered by failed deploys.
    private const int DailyCallBudget = 90;
    private static readonly TimeSpan BudgetWindow = TimeSpan.FromHours(24);

    private static readonly DateTime TournamentCutoffUtc = new(2026, 7, 20, 23, 59, 59, DateTimeKind.Utc);
    private static readonly StringComparer StageComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Group stage is split into three rounds (group-1/2/3) so each round can lock independently.</summary>
    private static bool IsGroupStage(string? stage) =>
        stage is not null && stage.StartsWith("group", StringComparison.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan sleepFor = MaxSleep;

            try
            {
                if (DateTime.UtcNow > TournamentCutoffUtc)
                {
                    logger.LogInformation("Tournament cutoff reached. Stopping result polling service.");
                    break;
                }

                sleepFor = await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while fetching results.");
            }

            if (sleepFor < MinSleep) sleepFor = MinSleep;
            if (sleepFor > MaxSleep) sleepFor = MaxSleep;

            try
            {
                await Task.Delay(sleepFor, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs one polling cycle. Returns the duration to sleep before the next cycle,
    /// computed from the schedule + pending retries so we wake only when needed.
    /// </summary>
    private async Task<TimeSpan> RunCycleAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scoringService = scope.ServiceProvider.GetRequiredService<ScoringService>();

        var existingResults = (await dbContext.MatchResults
                .Select(r => r.MatchId)
                .ToListAsync(ct))
            .ToHashSet();

        var pendingFetches = await dbContext.PendingMatchFetches
            .ToDictionaryAsync(p => p.MatchId, ct);

        var schedule = scheduleProvider.Current;
        var allMatches = schedule.GetAllMatches();

        // Build the set of matches whose results we still owe ourselves.
        // For each: compute the earliest time we should poll next.
        var outstanding = new List<(MatchEntry Match, DateTime NextPollAt, PendingMatchFetch? Pending)>();
        foreach (var match in allMatches)
        {
            if (existingResults.Contains(match.Id)) continue;
            if (match.AreTeamsUndetermined) continue; // can't fetch until fixture is locked

            pendingFetches.TryGetValue(match.Id, out var pending);

            var expectedReady = match.Date + GetBufferForStage(match.Stage);
            var nextPollAt = pending?.NextAttemptAt ?? expectedReady;

            // Already exhausted retries — leave it alone until manual intervention.
            if (pending is not null && pending.AttemptCount >= MaxFetchAttempts)
            {
                continue;
            }

            outstanding.Add((match, nextPollAt, pending));
        }

        var dueMatches = outstanding
            .Where(x => x.NextPollAt <= now.Add(DueTolerance))
            .Select(x => x.Match)
            .ToList();

        if (dueMatches.Count == 0)
        {
            return ComputeSleep(outstanding, now);
        }

        // ---- Budget gate ----
        if (!await BudgetAllowsCallAsync(dbContext, now, ct))
        {
            logger.LogWarning(
                "Daily WC2026 API budget exhausted ({Budget}/24h). Deferring poll for {Match} matches.",
                DailyCallBudget,
                dueMatches.Count);
            // Sleep ~1h and re-evaluate; the rolling window will free up by then.
            return TimeSpan.FromHours(1);
        }

        // ---- One coalesced call covers all due matches ----
        await LogApiCallAsync(dbContext, "/matches?status=completed", now, ct);
        var completedMatches = await apiClient.GetCompletedMatchesAsync(ct);

        var foundMatchIds = new HashSet<int>();
        var newKnockoutResult = false;
        var newResults = 0;
        var announcements = new List<(int MatchId, int HomeScore, int AwayScore, string? Referee)>();

        foreach (var dto in completedMatches)
        {
            if (dto.Score?.Ft is not [var homeScore, var awayScore]) continue;

            var matchId = apiClient.MapToLocalMatchId(dto.KickoffAt, schedule);
            if (matchId is null || existingResults.Contains(matchId.Value)) continue;

            var localMatch = schedule.GetMatch(matchId.Value);
            var refereeName = string.IsNullOrWhiteSpace(dto.Referee) ? null : dto.Referee.Trim();

            dbContext.MatchResults.Add(new MatchResult
            {
                Id = Guid.NewGuid(),
                MatchId = matchId.Value,
                HomeScore = homeScore,
                AwayScore = awayScore,
                FetchedAt = DateTime.UtcNow,
                Referee = refereeName
            });

            var predictions = await dbContext.Predictions
                .Where(prediction => prediction.MatchId == matchId.Value)
                .ToListAsync(ct);

            foreach (var prediction in predictions)
            {
                prediction.Points = scoringService.CalculatePoints(
                    prediction.HomeScore,
                    prediction.AwayScore,
                    homeScore,
                    awayScore);
            }

            // Result obtained — drop any pending retry row.
            if (pendingFetches.TryGetValue(matchId.Value, out var pendingToRemove))
            {
                dbContext.PendingMatchFetches.Remove(pendingToRemove);
            }

            existingResults.Add(matchId.Value);
            foundMatchIds.Add(matchId.Value);
            newResults++;
            announcements.Add((matchId.Value, homeScore, awayScore, refereeName));

            if (localMatch is not null && !IsGroupStage(localMatch.Stage))
            {
                newKnockoutResult = true;
            }
        }

        // For matches that were due but did NOT show up in this response, schedule a retry.
        foreach (var match in dueMatches)
        {
            if (foundMatchIds.Contains(match.Id)) continue;

            logger.LogError(
                "No result available from WC2026 API for due match {MatchId} ({Stage}, kickoff {Kickoff:o}).",
                match.Id,
                match.Stage,
                match.Date);

            if (pendingFetches.TryGetValue(match.Id, out var pending))
            {
                pending.AttemptCount += 1;
                pending.NextAttemptAt = now + RetryInterval;
                if (pending.AttemptCount >= MaxFetchAttempts)
                {
                    logger.LogError(
                        "Match {MatchId} exhausted {Attempts} fetch attempts without a result. Giving up; manual intervention required.",
                        match.Id,
                        pending.AttemptCount);
                }
            }
            else
            {
                dbContext.PendingMatchFetches.Add(new PendingMatchFetch
                {
                    MatchId = match.Id,
                    FirstScheduledAt = match.Date + GetBufferForStage(match.Stage),
                    NextAttemptAt = now + RetryInterval,
                    AttemptCount = 1
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);

        if (announcements.Count > 0)
        {
            var announcer = scope.ServiceProvider.GetRequiredService<ResultAnnouncementService>();
            foreach (var (annMatchId, annHome, annAway, annReferee) in announcements)
            {
                try
                {
                    await announcer.AnnounceResultAsync(annMatchId, annHome, annAway, annReferee, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to post Dommeren chat announcement for match {MatchId}.",
                        annMatchId);
                }
            }
        }

        logger.LogInformation(
            "Result poll complete: {NewResults} new (of {Due} due). Knockout result: {Knockout}.",
            newResults,
            dueMatches.Count,
            newKnockoutResult);

        // Only burn a second API call if we actually got a knockout result that may
        // have unlocked the next round's fixtures.
        if (newKnockoutResult && HasUndeterminedKnockoutMatches(schedule))
        {
            if (await BudgetAllowsCallAsync(dbContext, DateTime.UtcNow, ct))
            {
                await LogApiCallAsync(dbContext, "/matches?status=scheduled", DateTime.UtcNow, ct);
                await dbContext.SaveChangesAsync(ct);
                await CheckForFixtureUpdatesAsync(ct);
            }
            else
            {
                logger.LogWarning("Skipping fixture update — daily API budget exhausted.");
            }
        }

        // Recompute outstanding (some may have been resolved) for next-sleep calc.
        var stillOutstanding = outstanding
            .Where(x => !foundMatchIds.Contains(x.Match.Id))
            .Select(x =>
            {
                // Refresh next-poll for due-but-not-found matches with new retry time.
                if (dueMatches.Any(d => d.Id == x.Match.Id))
                {
                    return (x.Match, now + RetryInterval, x.Pending);
                }
                return x;
            })
            .ToList();

        return ComputeSleep(stillOutstanding, DateTime.UtcNow);
    }

    private static TimeSpan ComputeSleep(
        IReadOnlyList<(MatchEntry Match, DateTime NextPollAt, PendingMatchFetch? Pending)> outstanding,
        DateTime now)
    {
        if (outstanding.Count == 0) return MaxSleep;

        var nextWake = outstanding.Min(x => x.NextPollAt);
        var delta = nextWake - now;
        return delta <= TimeSpan.Zero ? MinSleep : delta;
    }

    private static TimeSpan GetBufferForStage(string stage) => stage?.ToLowerInvariant() switch
    {
        "group-1" or "group-2" or "group-3" => GroupStageBuffer,
        "final" => FinalBuffer,
        "round-of-32" or "round-of-16" or "quarter-final" or "semi-final" or "third-place" => KnockoutBuffer,
        _ => KnockoutBuffer
    };

    private static bool HasUndeterminedKnockoutMatches(MatchSchedule schedule) =>
        schedule.GetAllMatches().Any(m =>
            m.AreTeamsUndetermined
            && !m.ManualOverride
            && !IsGroupStage(m.Stage));

    private static async Task<bool> BudgetAllowsCallAsync(AppDbContext dbContext, DateTime now, CancellationToken ct)
    {
        var windowStart = now - BudgetWindow;
        var callsInWindow = await dbContext.ApiCallLogs
            .CountAsync(log => log.CalledAt >= windowStart, ct);
        return callsInWindow < DailyCallBudget;
    }

    private static Task LogApiCallAsync(AppDbContext dbContext, string endpoint, DateTime calledAt, CancellationToken ct)
    {
        dbContext.ApiCallLogs.Add(new ApiCallLog
        {
            Id = Guid.NewGuid(),
            CalledAt = calledAt,
            Endpoint = endpoint
        });
        return dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fetches scheduled matches from upstream and rewrites matches.json for any knockout
    /// fixture whose teams have just been determined. Kept internal-accessible so existing
    /// reflection-based tests continue to work.
    /// </summary>
    private async Task CheckForFixtureUpdatesAsync(CancellationToken ct)
    {
        var currentSchedule = scheduleProvider.Current;
        var currentMatches = currentSchedule.GetAllMatches();
        var undeterminedMatchesById = currentMatches
            .Where(match =>
                match.AreTeamsUndetermined
                && !match.ManualOverride
                && !IsGroupStage(match.Stage))
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
