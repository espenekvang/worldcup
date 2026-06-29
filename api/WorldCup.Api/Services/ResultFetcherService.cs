using System.Text.RegularExpressions;
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

    // Cadence for re-polling the upstream "scheduled" feed while knockout fixtures whose
    // feeder matches are all complete still have no teams (e.g. the bracket has not been
    // published yet in the minutes after the final group game). In-memory; resets on restart.
    private static readonly TimeSpan FixturePollRetryInterval = TimeSpan.FromMinutes(20);
    private DateTime _nextFixturePollAtUtc = DateTime.MinValue;

    // Cadence for re-polling the upstream "completed" feed to backfill teams for knockout
    // fixtures that already have a stored result but whose teams are still null. The scheduled
    // feed cannot help here (a played game has left it), so this is the only path that can
    // un-freeze such a fixture from its placeholder. In-memory; resets on restart.
    private static readonly TimeSpan CompletedBackfillRetryInterval = TimeSpan.FromMinutes(20);
    private DateTime _nextCompletedBackfillAtUtc = DateTime.MinValue;

    // Matches the "kamp NN" reference inside a knockout placeholder ("Vinner kamp 73",
    // "Taper kamp 101") so we can tell which earlier games a fixture feeds from.
    private static readonly Regex FeederMatchPattern =
        new(@"kamp\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches the "gruppe X" (or "gruppe A/B/C/D/F") reference inside a round-of-32 placeholder
    // so we can tell which groups a fixture's slots depend on.
    private static readonly Regex GroupRefPattern =
        new(@"gruppe\s+([A-L](?:\s*/\s*[A-L])*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        await ResetExhaustedKnockoutFetchesAsync(stoppingToken);

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
    /// On startup, resets any <see cref="PendingMatchFetch"/> rows that exhausted their
    /// retries for knockout matches with no stored result. These rows were written by a
    /// previous service version that required teams to be known before polling — but a
    /// deployed fix now fills in teams from the completed-matches feed, so the old "give up"
    /// state is stale. Resetting gives those matches a clean retry in the first cycle.
    /// </summary>
    private async Task ResetExhaustedKnockoutFetchesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingResultIds = (await dbContext.MatchResults
                    .Select(r => r.MatchId)
                    .ToListAsync(ct))
                .ToHashSet();

            var knockoutMatchIds = scheduleProvider.Current
                .GetAllMatches()
                .Where(m => !IsGroupStage(m.Stage))
                .Select(m => m.Id)
                .ToHashSet();

            var stale = await dbContext.PendingMatchFetches
                .Where(p => p.AttemptCount >= MaxFetchAttempts
                    && knockoutMatchIds.Contains(p.MatchId)
                    && !existingResultIds.Contains(p.MatchId))
                .ToListAsync(ct);

            if (stale.Count == 0) return;

            foreach (var p in stale)
            {
                p.AttemptCount = 0;
                p.NextAttemptAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation(
                "Startup: reset {Count} exhausted PendingMatchFetch row(s) for knockout matches with no result — they will be retried this cycle.",
                stale.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup: could not reset exhausted knockout pending fetches — will proceed normally.");
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

            // For group-stage matches we always know the teams, so AreTeamsUndetermined
            // is a genuine data problem and we skip. For knockout matches we skip only
            // while the buffer window has not yet elapsed — once the game should be
            // finished the completed-matches feed will carry the teams, and we can fill
            // them in and store the result in the same cycle (see below).
            if (match.AreTeamsUndetermined && IsGroupStage(match.Stage)) continue;
            if (match.AreTeamsUndetermined)
            {
                var wouldBeReady = match.Date + GetBufferForStage(match.Stage);
                if (now < wouldBeReady) continue;
            }

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
            // No match results are due, but knockout fixtures may have become resolvable in an
            // earlier cycle (e.g. the final group game's result has already landed). Try to pull
            // in the teams from the scheduled feed (for not-yet-played fixtures) and from the
            // completed feed (for already-played fixtures that are still missing their teams),
            // then sleep until the next result *or* re-poll is due.
            await TryResolveFixturesAsync(dbContext, existingResults, now, ct);
            await TryBackfillPlayedKnockoutTeamsAsync(dbContext, existingResults, now, ct);
            return ComputeSleep(outstanding, DateTime.UtcNow, NextResolutionWake(existingResults));
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
        var newResults = 0;

        // Knockout fixtures whose teams were null but are now known from the completed feed.
        // Written to matches.json after we finish processing results so the schedule stays
        // consistent for the rest of this cycle.
        var teamFillsFromCompleted = new Dictionary<int, MatchEntry>();

        foreach (var dto in completedMatches)
        {
            if (dto.HomeScore is not { } homeScore || dto.AwayScore is not { } awayScore)
            {
                logger.LogWarning(
                    "WC2026 API returned a completed match (matchNumber {MatchNumber}, kickoff {Kickoff:o}) with no parseable score. Skipping.",
                    dto.MatchNumber,
                    dto.KickoffAt);
                continue;
            }

            var matchId = apiClient.MapToLocalMatchId(dto.MatchNumber, dto.KickoffAt, schedule);
            if (matchId is null)
            {
                logger.LogWarning(
                    "WC2026 API returned a completed match (matchNumber {MatchNumber}, kickoff {Kickoff:o}, score {Home}-{Away}) that did not map to any local fixture within the time window. Skipping.",
                    dto.MatchNumber,
                    dto.KickoffAt,
                    homeScore,
                    awayScore);
                continue;
            }

            // If the local knockout fixture still has null teams, fill them in from the
            // completed-match DTO. This self-heals the case where the scheduled feed was
            // never polled in time (rate-limit, restart, upstream delay) — by the time
            // results are available the completed feed carries both teams and the score.
            var localMatch = schedule.GetMatch(matchId.Value);
            if (localMatch is not null && localMatch.AreTeamsUndetermined && !IsGroupStage(localMatch.Stage))
            {
                var homeCode = ResolveTeamCode(dto.HomeCode, dto.Home);
                var awayCode = ResolveTeamCode(dto.AwayCode, dto.Away);
                if (homeCode is not null || awayCode is not null)
                {
                    teamFillsFromCompleted[matchId.Value] = new MatchEntry
                    {
                        Id = localMatch.Id,
                        Date = localMatch.Date,
                        Stage = localMatch.Stage,
                        HomeTeam = homeCode ?? localMatch.HomeTeam,
                        AwayTeam = awayCode ?? localMatch.AwayTeam,
                        HomePlaceholder = localMatch.HomePlaceholder,
                        AwayPlaceholder = localMatch.AwayPlaceholder,
                        Group = localMatch.Group,
                        VenueId = localMatch.VenueId,
                        ManualOverride = localMatch.ManualOverride
                    };
                    logger.LogInformation(
                        "Filling in teams for knockout fixture {MatchId} ({Stage}) from completed-matches feed: {Home} vs {Away}.",
                        matchId.Value,
                        localMatch.Stage,
                        homeCode ?? "(unchanged)",
                        awayCode ?? "(unchanged)");
                }
            }

            if (existingResults.Contains(matchId.Value)) continue;

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
        }

        // Persist any team fills extracted from the completed-matches feed.
        if (teamFillsFromCompleted.Count > 0)
        {
            var currentScheduleMatches = scheduleProvider.Current.GetAllMatches();
            var patchedMatches = currentScheduleMatches
                .Select(m => teamFillsFromCompleted.TryGetValue(m.Id, out var filled) ? filled : m)
                .ToList();
            await matchFileWriter.WriteAsync(patchedMatches, ct);
            logger.LogInformation(
                "Wrote team fills for {Count} completed knockout fixture(s) to matches.json.",
                teamFillsFromCompleted.Count);
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

        logger.LogInformation(
            "Result poll complete: {NewResults} new (of {Due} due).",
            newResults,
            dueMatches.Count);

        // The new results may have completed a knockout fixture's feeders: the final group game
        // finalises the round-of-32 bracket, a round-of-32 result finalises a round-of-16 tie,
        // and so on. Pull in any teams that just became known. (existingResults already includes
        // the results added above.)
        await TryResolveFixturesAsync(dbContext, existingResults, DateTime.UtcNow, ct);

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

        return ComputeSleep(stillOutstanding, DateTime.UtcNow, NextResolutionWake(existingResults));
    }

    private static TimeSpan ComputeSleep(
        IReadOnlyList<(MatchEntry Match, DateTime NextPollAt, PendingMatchFetch? Pending)> outstanding,
        DateTime now,
        DateTime? fixturePollWake = null)
    {
        DateTime? nextWake = outstanding.Count > 0 ? outstanding.Min(x => x.NextPollAt) : null;

        if (fixturePollWake is { } fixtureWake)
        {
            nextWake = nextWake is { } existing && existing < fixtureWake ? existing : fixtureWake;
        }

        if (nextWake is null) return MaxSleep;

        var delta = nextWake.Value - now;
        return delta <= TimeSpan.Zero ? MinSleep : delta;
    }

    private static TimeSpan GetBufferForStage(string stage) => stage?.ToLowerInvariant() switch
    {
        "group-1" or "group-2" or "group-3" => GroupStageBuffer,
        "final" => FinalBuffer,
        "round-of-32" or "round-of-16" or "quarter-final" or "semi-final" or "third-place" => KnockoutBuffer,
        _ => KnockoutBuffer
    };

    /// <summary>
    /// Polls the upstream "scheduled" feed and writes any now-known knockout teams into
    /// matches.json — but only when at least one undetermined knockout fixture has had all of its
    /// feeder matches played (so the upstream bracket should be populated), and never more often
    /// than <see cref="FixturePollRetryInterval"/>. The bracket can lag the final feeder result,
    /// so we retry on a backoff until every fixture is resolved.
    /// </summary>
    private async Task TryResolveFixturesAsync(
        AppDbContext dbContext,
        IReadOnlySet<int> completedMatchIds,
        DateTime now,
        CancellationToken ct)
    {
        var resolvable = GetResolvableUndeterminedKnockout(scheduleProvider.Current, completedMatchIds);
        if (resolvable.Count == 0) return;
        if (now < _nextFixturePollAtUtc) return;

        // Back off regardless of outcome so a slow-to-publish bracket or an exhausted budget
        // cannot turn into a tight poll loop.
        _nextFixturePollAtUtc = now + FixturePollRetryInterval;

        if (!await BudgetAllowsCallAsync(dbContext, now, ct))
        {
            logger.LogWarning(
                "Skipping fixture-resolution poll — daily API budget exhausted. {Count} knockout fixture(s) awaiting teams.",
                resolvable.Count);
            return;
        }

        logger.LogInformation(
            "Feeders complete for {Count} undetermined knockout fixture(s) — polling upstream for resolved teams.",
            resolvable.Count);

        await LogApiCallAsync(dbContext, "/matches?status=scheduled", now, ct);
        await CheckForFixtureUpdatesAsync(ct);
    }

    /// <summary>
    /// The undetermined, non-overridden knockout fixtures whose feeder matches have all been
    /// played — i.e. the ones the upstream bracket should now be able to fill in.
    /// </summary>
    private static List<MatchEntry> GetResolvableUndeterminedKnockout(
        MatchSchedule schedule,
        IReadOnlySet<int> completedMatchIds)
    {
        var resolvable = new List<MatchEntry>();
        foreach (var match in schedule.GetAllMatches())
        {
            if (IsGroupStage(match.Stage)) continue;
            if (!match.AreTeamsUndetermined) continue;
            if (match.ManualOverride) continue;
            // A fixture that has itself already been played has left the scheduled feed, so the
            // scheduled-feed resolver can no longer see it. The completed-feed backfill
            // (TryBackfillPlayedKnockoutTeamsAsync) is responsible for those instead.
            if (completedMatchIds.Contains(match.Id)) continue;
            if (GetFeederMatchIds(match, schedule).All(completedMatchIds.Contains))
            {
                resolvable.Add(match);
            }
        }
        return resolvable;
    }

    /// <summary>
    /// The match Ids a knockout fixture feeds from. Later rounds name specific games
    /// ("Vinner kamp 73"). The round of 32 instead names group positions: a single-group slot
    /// ("Vinner gruppe E", "2. plass gruppe C") is fixed as soon as that group finishes — so e.g.
    /// "2. plass gruppe A vs 2. plass gruppe B" can be filled in once groups A and B are done,
    /// without waiting for the rest of the group stage. A best-third slot instead lists several
    /// groups ("3. plass gruppe A/B/C/D/F") and can only be filled once the third-place ranking
    /// across every group is known, so it depends on all group matches.
    /// </summary>
    private static IReadOnlyCollection<int> GetFeederMatchIds(MatchEntry match, MatchSchedule schedule)
    {
        var referencedMatches = ExtractFeederMatchIds(match.HomePlaceholder)
            .Concat(ExtractFeederMatchIds(match.AwayPlaceholder))
            .ToHashSet();

        if (referencedMatches.Count > 0) return referencedMatches;

        var feederGroups = GetFeederGroups(match);

        var groupMatchIds = schedule.GetAllMatches()
            .Where(m => IsGroupStage(m.Stage)
                && (feederGroups is null || (m.Group is { } group && feederGroups.Contains(group))))
            .Select(m => m.Id)
            .ToHashSet();

        // Defensive: never treat a fixture as resolvable off an empty feeder set (which All()
        // would vacuously satisfy). Fall back to depending on the whole group stage.
        return groupMatchIds.Count > 0
            ? groupMatchIds
            : schedule.GetAllMatches().Where(m => IsGroupStage(m.Stage)).Select(m => m.Id).ToHashSet();
    }

    /// <summary>
    /// The group letters a round-of-32 fixture depends on, or <c>null</c> when it contains a
    /// best-third slot (which needs the full cross-group third-place ranking, i.e. every group).
    /// </summary>
    private static HashSet<string>? GetFeederGroups(MatchEntry match)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var placeholder in new[] { match.HomePlaceholder, match.AwayPlaceholder })
        {
            if (string.IsNullOrWhiteSpace(placeholder)) continue;

            foreach (Match m in GroupRefPattern.Matches(placeholder))
            {
                var letters = m.Groups[1].Value
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                // A slot naming several groups is the best-third wildcard → depends on all groups.
                if (letters.Length > 1) return null;

                groups.Add(letters[0]);
            }
        }

        return groups.Count > 0 ? groups : null;
    }

    private static IEnumerable<int> ExtractFeederMatchIds(string? placeholder)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) yield break;

        foreach (Match m in FeederMatchPattern.Matches(placeholder))
        {
            if (int.TryParse(m.Groups[1].Value, out var id))
            {
                yield return id;
            }
        }
    }

    /// <summary>Next wake-up for the fixture re-poll, or null when nothing is awaiting teams.</summary>
    private DateTime? NextFixturePollWake(IReadOnlySet<int> completedMatchIds) =>
        GetResolvableUndeterminedKnockout(scheduleProvider.Current, completedMatchIds).Count > 0
            ? _nextFixturePollAtUtc
            : null;

    /// <summary>
    /// Backfills teams for knockout fixtures whose result has already landed but whose teams are
    /// still null. Such a fixture is invisible to <see cref="TryResolveFixturesAsync"/> (it has
    /// left the scheduled feed by being played) and is no longer "due" for a result, so without
    /// this it stays frozen on its placeholder ("Vinner kamp 89") forever in the UI. We poll the
    /// completed feed — which still carries both teams — on a backoff, guarded by the daily
    /// budget, and write any resolved teams to matches.json.
    /// </summary>
    private async Task TryBackfillPlayedKnockoutTeamsAsync(
        AppDbContext dbContext,
        IReadOnlySet<int> completedMatchIds,
        DateTime now,
        CancellationToken ct)
    {
        if (!HasPlayedUndeterminedKnockout(completedMatchIds)) return;
        if (now < _nextCompletedBackfillAtUtc) return;

        // Back off regardless of outcome so a feed that never carries the teams (or an exhausted
        // budget) cannot turn into a tight poll loop.
        _nextCompletedBackfillAtUtc = now + CompletedBackfillRetryInterval;

        if (!await BudgetAllowsCallAsync(dbContext, now, ct))
        {
            logger.LogWarning(
                "Skipping completed-feed team backfill — daily API budget exhausted. Played knockout fixture(s) still missing teams.");
            return;
        }

        logger.LogInformation(
            "Played knockout fixture(s) have a result but no teams — polling completed feed to backfill names.");

        await LogApiCallAsync(dbContext, "/matches?status=completed", now, ct);
        await BackfillCompletedKnockoutTeamsAsync(ct);
    }

    /// <summary>
    /// Polls the completed-matches feed and writes any now-known teams into matches.json for
    /// knockout fixtures that are still undetermined. Mirrors <see cref="CheckForFixtureUpdatesAsync"/>
    /// but reads the *completed* feed, so it resolves fixtures whose game has already been played.
    /// Returns the number of fixtures updated. Kept internal-accessible so reflection-based tests
    /// can exercise it directly.
    /// </summary>
    private async Task<int> BackfillCompletedKnockoutTeamsAsync(CancellationToken ct)
    {
        var currentSchedule = scheduleProvider.Current;
        var currentMatches = currentSchedule.GetAllMatches();
        var undeterminedById = currentMatches
            .Where(match =>
                match.AreTeamsUndetermined
                && !match.ManualOverride
                && !IsGroupStage(match.Stage))
            .ToDictionary(match => match.Id);

        if (undeterminedById.Count == 0) return 0;

        var completedMatches = await apiClient.GetCompletedMatchesAsync(ct);
        var updatesById = new Dictionary<int, MatchEntry>();

        foreach (var dto in completedMatches)
        {
            var matchId = apiClient.MapToLocalMatchId(dto.MatchNumber, dto.KickoffAt, currentSchedule);
            if (matchId is null || !undeterminedById.TryGetValue(matchId.Value, out var localMatch))
            {
                continue;
            }

            var homeTeamCode = ResolveTeamCode(dto.HomeCode, dto.Home);
            var awayTeamCode = ResolveTeamCode(dto.AwayCode, dto.Away);
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
            logger.LogWarning("Completed-feed team backfill found no resolvable teams for the played knockout fixture(s).");
            return 0;
        }

        var updatedMatches = currentMatches
            .Select(match => updatesById.TryGetValue(match.Id, out var updatedMatch) ? updatedMatch : match)
            .ToList();

        await matchFileWriter.WriteAsync(updatedMatches, ct);
        logger.LogInformation(
            "Completed-feed backfill resolved teams for {Count} played knockout fixture(s).",
            updatesById.Count);
        return updatesById.Count;
    }

    /// <summary>True when a knockout fixture has a stored result but still no teams.</summary>
    private bool HasPlayedUndeterminedKnockout(IReadOnlySet<int> completedMatchIds) =>
        scheduleProvider.Current.GetAllMatches().Any(m =>
            !IsGroupStage(m.Stage)
            && m.AreTeamsUndetermined
            && !m.ManualOverride
            && completedMatchIds.Contains(m.Id));

    /// <summary>Next wake-up for the completed-feed backfill, or null when nothing needs it.</summary>
    private DateTime? NextBackfillWake(IReadOnlySet<int> completedMatchIds) =>
        HasPlayedUndeterminedKnockout(completedMatchIds) ? _nextCompletedBackfillAtUtc : null;

    /// <summary>
    /// Earliest pending team-resolution wake-up across both paths (scheduled-feed fixture poll
    /// and completed-feed backfill), or null when neither is pending.
    /// </summary>
    private DateTime? NextResolutionWake(IReadOnlySet<int> completedMatchIds)
    {
        var fixtureWake = NextFixturePollWake(completedMatchIds);
        var backfillWake = NextBackfillWake(completedMatchIds);

        if (fixtureWake is null) return backfillWake;
        if (backfillWake is null) return fixtureWake;
        return fixtureWake < backfillWake ? fixtureWake : backfillWake;
    }

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
    /// Resolves a fixture's team to a local three-letter code. The API's <c>*_team_code</c>
    /// already matches our teams.json keys, so it is used directly; the full team name is a
    /// fallback for the rare case the code is missing.
    /// </summary>
    private string? ResolveTeamCode(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code)) return code.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : teamCodeMapper.GetCode(name);
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
            var matchId = apiClient.MapToLocalMatchIdByMatchNumber(dto.MatchNumber, dto.KickoffAt, currentSchedule);
            if (matchId is null || !undeterminedMatchesById.TryGetValue(matchId.Value, out var localMatch) || !localMatch.AreTeamsUndetermined)
            {
                continue;
            }

            // Prefer the API's three-letter code (aligns with teams.json keys); fall back to
            // mapping the full team name only when the code is absent.
            var homeTeamCode = ResolveTeamCode(dto.HomeCode, dto.Home);
            var awayTeamCode = ResolveTeamCode(dto.AwayCode, dto.Away);

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
