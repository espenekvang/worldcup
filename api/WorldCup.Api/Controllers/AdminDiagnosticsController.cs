using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Models;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminDiagnosticsController(
    AppDbContext dbContext,
    MatchScheduleProvider scheduleProvider,
    Wc2026ApiClient apiClient,
    TeamCodeMapper teamCodeMapper,
    MatchFileWriter matchFileWriter,
    ScoringService scoringService,
    ILogger<AdminDiagnosticsController> logger) : ControllerBase
{
    private const int MaxFetchAttempts = 5;
    private const int DailyCallBudget = 90;
    private static readonly TimeSpan BudgetWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan GroupStageBuffer = TimeSpan.FromHours(2.10);
    private static readonly TimeSpan KnockoutBuffer = TimeSpan.FromHours(3.25);
    private static readonly TimeSpan FinalBuffer = TimeSpan.FromHours(3.5);

    private static readonly Regex FeederMatchPattern =
        new(@"kamp\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GroupRefPattern =
        new(@"gruppe\s+([A-L](?:\s*/\s*[A-L])*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns a snapshot of the result-fetcher's health: which matches are overdue without a
    /// stored result, and which knockout fixtures still have null teams. Use this to decide
    /// whether a manual force-fetch or team override is needed.
    /// </summary>
    [HttpGet("/api/admin/diagnostics/missing-results")]
    public async Task<ActionResult<DiagnosticsResponse>> GetMissingResults(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var schedule = scheduleProvider.Current;
        var allMatches = schedule.GetAllMatches();

        var existingResultMatchIds = (await dbContext.MatchResults
                .Select(r => r.MatchId)
                .ToListAsync(ct))
            .ToHashSet();

        var pendingFetches = await dbContext.PendingMatchFetches
            .ToDictionaryAsync(p => p.MatchId, ct);

        var windowStart = now - BudgetWindow;
        var callsInWindow = await dbContext.ApiCallLogs
            .CountAsync(log => log.CalledAt >= windowStart, ct);

        var budget = new ApiBudgetInfo(
            UsedInLast24h: callsInWindow,
            DailyBudget: DailyCallBudget,
            Remaining: Math.Max(0, DailyCallBudget - callsInWindow));

        // Matches past their expected-ready time with no stored result.
        // Includes null-team knockout matches (the fixed fetcher now handles them too).
        var missingResults = new List<MissingResultInfo>();
        foreach (var match in allMatches.OrderBy(m => m.Date))
        {
            if (existingResultMatchIds.Contains(match.Id)) continue;
            if (match.AreTeamsUndetermined && IsGroupStage(match.Stage)) continue;

            var buffer = GetBufferForStage(match.Stage);
            var expectedReadyAt = match.Date + buffer;
            if (now < expectedReadyAt) continue;

            pendingFetches.TryGetValue(match.Id, out var pending);
            var attempts = pending?.AttemptCount ?? 0;
            var exhausted = attempts >= MaxFetchAttempts;

            missingResults.Add(new MissingResultInfo(
                MatchId: match.Id,
                Stage: match.Stage,
                KickoffAt: match.Date,
                HomeTeam: match.HomeTeam,
                AwayTeam: match.AwayTeam,
                TeamsUnknown: match.AreTeamsUndetermined,
                ExpectedReadyAt: expectedReadyAt,
                PendingAttempts: attempts,
                Exhausted: exhausted,
                NextAttemptAt: exhausted ? null : pending?.NextAttemptAt));
        }

        // Knockout fixtures whose teams are still null.
        var completedMatchIds = existingResultMatchIds;
        var unresolvedFixtures = new List<UnresolvedFixtureInfo>();
        foreach (var match in allMatches.OrderBy(m => m.Date))
        {
            if (IsGroupStage(match.Stage)) continue;
            if (!match.AreTeamsUndetermined) continue;

            var feederMatchIds = GetFeederMatchIds(match, schedule);
            var waitingFor = feederMatchIds
                .Where(id => !completedMatchIds.Contains(id))
                .OrderBy(id => id)
                .ToList();

            var status = waitingFor.Count == 0 ? "resolvable" : "waiting_for_feeders";

            unresolvedFixtures.Add(new UnresolvedFixtureInfo(
                MatchId: match.Id,
                Stage: match.Stage,
                KickoffAt: match.Date,
                HomePlaceholder: match.HomePlaceholder,
                AwayPlaceholder: match.AwayPlaceholder,
                Status: status,
                WaitingForMatchIds: waitingFor));
        }

        return Ok(new DiagnosticsResponse(
            AsOf: now,
            ApiCallBudget: budget,
            MissingResults: missingResults,
            UnresolvedFixtures: unresolvedFixtures));
    }

    /// <summary>
    /// Immediately fetches results and resolves fixture teams from the upstream API,
    /// bypassing the in-app daily budget gate. Use when the automatic background service
    /// is stuck due to budget exhaustion. Still logs calls to ApiCallLogs so the
    /// rolling budget window stays accurate; the upstream provider enforces its own limit.
    /// </summary>
    [HttpPost("/api/admin/diagnostics/force-fetch")]
    public async Task<ActionResult<ForceFetchResult>> ForceFetch(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var schedule = scheduleProvider.Current;
        var allMatches = schedule.GetAllMatches();

        var existingResultIds = (await dbContext.MatchResults
                .Select(r => r.MatchId)
                .ToListAsync(ct))
            .ToHashSet();

        // ── Step 1: completed-matches feed → results + null-team fills ──────────
        await LogApiCallAsync("/matches?status=completed", now, ct);
        var completedDtos = await apiClient.GetCompletedMatchesAsync(ct);

        var newResults = 0;
        var teamFills = new Dictionary<int, MatchEntry>();

        foreach (var dto in completedDtos)
        {
            if (dto.HomeScore is not { } homeScore || dto.AwayScore is not { } awayScore) continue;

            var matchId = apiClient.MapToLocalMatchId(dto.MatchNumber, dto.KickoffAt, schedule);
            if (matchId is null) continue;

            var localMatch = schedule.GetMatch(matchId.Value);
            if (localMatch is not null && localMatch.AreTeamsUndetermined && !IsGroupStage(localMatch.Stage))
            {
                var homeCode = ResolveTeamCode(dto.HomeCode, dto.Home);
                var awayCode = ResolveTeamCode(dto.AwayCode, dto.Away);
                if (homeCode is not null || awayCode is not null)
                {
                    var filledHome = homeCode ?? localMatch.HomeTeam;
                    var filledAway = awayCode ?? localMatch.AwayTeam;

                    if (MatchEntry.WouldDuplicateTeam(filledHome, filledAway))
                    {
                        logger.LogWarning(
                            "force-fetch: refusing to fill knockout fixture {MatchId} ({Stage}) from completed feed — " +
                            "both sides resolve to {Team}. Likely a mapping error; leaving teams unresolved.",
                            matchId.Value, localMatch.Stage, filledHome);
                    }
                    else
                    {
                        teamFills[matchId.Value] = new MatchEntry
                        {
                            Id = localMatch.Id,
                            Date = localMatch.Date,
                            Stage = localMatch.Stage,
                            HomeTeam = filledHome,
                            AwayTeam = filledAway,
                            HomePlaceholder = localMatch.HomePlaceholder,
                            AwayPlaceholder = localMatch.AwayPlaceholder,
                            Group = localMatch.Group,
                            VenueId = localMatch.VenueId,
                            ManualOverride = localMatch.ManualOverride
                        };
                    }
                }
            }

            if (existingResultIds.Contains(matchId.Value)) continue;

            dbContext.MatchResults.Add(new MatchResult
            {
                Id = Guid.NewGuid(),
                MatchId = matchId.Value,
                HomeScore = homeScore,
                AwayScore = awayScore,
                FetchedAt = DateTime.UtcNow,
                Referee = string.IsNullOrWhiteSpace(dto.Referee) ? null : dto.Referee.Trim()
            });

            var predictions = await dbContext.Predictions
                .Where(p => p.MatchId == matchId.Value)
                .ToListAsync(ct);

            foreach (var prediction in predictions)
            {
                prediction.Points = scoringService.CalculatePoints(
                    prediction.HomeScore, prediction.AwayScore, homeScore, awayScore);
            }

            var pending = await dbContext.PendingMatchFetches
                .FirstOrDefaultAsync(p => p.MatchId == matchId.Value, ct);
            if (pending is not null) dbContext.PendingMatchFetches.Remove(pending);

            existingResultIds.Add(matchId.Value);
            newResults++;
        }

        if (teamFills.Count > 0)
        {
            var patched = allMatches
                .Select(m => teamFills.TryGetValue(m.Id, out var f) ? f : m)
                .ToList();
            await matchFileWriter.WriteAsync(patched, ct);
            schedule = scheduleProvider.Current;
            allMatches = schedule.GetAllMatches();
        }

        await dbContext.SaveChangesAsync(ct);

        // ── Step 2: scheduled-matches feed → undetermined fixture teams ──────────
        var undetermined = allMatches
            .Where(m => !IsGroupStage(m.Stage) && m.AreTeamsUndetermined && !m.ManualOverride)
            .ToDictionary(m => m.Id);

        var fixtureTeamFills = 0;

        if (undetermined.Count > 0)
        {
            await LogApiCallAsync("/matches?status=scheduled", DateTime.UtcNow, ct);
            var scheduledDtos = await apiClient.GetScheduledMatchesAsync(ct);

            var fixtureUpdates = new Dictionary<int, MatchEntry>();
            foreach (var dto in scheduledDtos)
            {
                var matchId = apiClient.MapToLocalMatchIdByMatchNumber(dto.MatchNumber, dto.KickoffAt, schedule);
                if (matchId is null || !undetermined.TryGetValue(matchId.Value, out var local)) continue;

                var homeCode = ResolveTeamCode(dto.HomeCode, dto.Home);
                var awayCode = ResolveTeamCode(dto.AwayCode, dto.Away);
                if (homeCode is null && awayCode is null) continue;

                var newHome = homeCode ?? local.HomeTeam;
                var newAway = awayCode ?? local.AwayTeam;
                if (newHome == local.HomeTeam && newAway == local.AwayTeam) continue;

                if (MatchEntry.WouldDuplicateTeam(newHome, newAway))
                {
                    logger.LogWarning(
                        "force-fetch: refusing to update knockout fixture {MatchId} ({Stage}) from scheduled feed — " +
                        "both sides resolve to {Team}. Likely a mapping error; leaving teams unresolved.",
                        local.Id, local.Stage, newHome);
                    continue;
                }

                fixtureUpdates[local.Id] = new MatchEntry
                {
                    Id = local.Id,
                    Date = local.Date,
                    Stage = local.Stage,
                    HomeTeam = newHome,
                    AwayTeam = newAway,
                    HomePlaceholder = local.HomePlaceholder,
                    AwayPlaceholder = local.AwayPlaceholder,
                    Group = local.Group,
                    VenueId = local.VenueId,
                    ManualOverride = local.ManualOverride
                };
            }

            if (fixtureUpdates.Count > 0)
            {
                var currentMatches = scheduleProvider.Current.GetAllMatches();
                var patchedMatches = currentMatches
                    .Select(m => fixtureUpdates.TryGetValue(m.Id, out var u) ? u : m)
                    .ToList();
                await matchFileWriter.WriteAsync(patchedMatches, ct);
                fixtureTeamFills = fixtureUpdates.Count;
            }

            await dbContext.SaveChangesAsync(ct);
        }

        return Ok(new ForceFetchResult(
            NewResults: newResults,
            TeamFillsFromCompleted: teamFills.Count,
            FixtureTeamFills: fixtureTeamFills,
            CompletedDtosReceived: completedDtos.Count,
            RemainingUndetermined: undetermined.Count - fixtureTeamFills));
    }

    private string? ResolveTeamCode(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code)) return code.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : teamCodeMapper.GetCode(name);
    }

    private async Task LogApiCallAsync(string endpoint, DateTime calledAt, CancellationToken ct)
    {
        dbContext.ApiCallLogs.Add(new ApiCallLog
        {
            Id = Guid.NewGuid(),
            CalledAt = calledAt,
            Endpoint = endpoint
        });
        await dbContext.SaveChangesAsync(ct);
    }

    private static bool IsGroupStage(string? stage) =>
        stage is not null && stage.StartsWith("group", StringComparison.OrdinalIgnoreCase);

    private static TimeSpan GetBufferForStage(string stage) => stage?.ToLowerInvariant() switch
    {
        "group-1" or "group-2" or "group-3" => GroupStageBuffer,
        "final" => FinalBuffer,
        _ => KnockoutBuffer
    };

    private static IReadOnlyCollection<int> GetFeederMatchIds(MatchEntry match, MatchSchedule schedule)
    {
        var referencedMatches = ExtractFeederMatchIds(match.HomePlaceholder)
            .Concat(ExtractFeederMatchIds(match.AwayPlaceholder))
            .ToHashSet();

        if (referencedMatches.Count > 0) return referencedMatches;

        var feederGroups = GetFeederGroups(match);

        var groupMatchIds = schedule.GetAllMatches()
            .Where(m => IsGroupStage(m.Stage)
                && (feederGroups is null || (m.Group is { } g && feederGroups.Contains(g))))
            .Select(m => m.Id)
            .ToHashSet();

        return groupMatchIds.Count > 0
            ? groupMatchIds
            : schedule.GetAllMatches().Where(m => IsGroupStage(m.Stage)).Select(m => m.Id).ToHashSet();
    }

    private static HashSet<string>? GetFeederGroups(MatchEntry match)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var placeholder in new[] { match.HomePlaceholder, match.AwayPlaceholder })
        {
            if (string.IsNullOrWhiteSpace(placeholder)) continue;

            foreach (System.Text.RegularExpressions.Match m in GroupRefPattern.Matches(placeholder))
            {
                var letters = m.Groups[1].Value
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (letters.Length > 1) return null;

                groups.Add(letters[0]);
            }
        }

        return groups.Count > 0 ? groups : null;
    }

    private static IEnumerable<int> ExtractFeederMatchIds(string? placeholder)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) yield break;

        foreach (System.Text.RegularExpressions.Match m in FeederMatchPattern.Matches(placeholder))
        {
            if (int.TryParse(m.Groups[1].Value, out var id))
            {
                yield return id;
            }
        }
    }
}

public sealed record DiagnosticsResponse(
    DateTime AsOf,
    ApiBudgetInfo ApiCallBudget,
    IReadOnlyList<MissingResultInfo> MissingResults,
    IReadOnlyList<UnresolvedFixtureInfo> UnresolvedFixtures);

public sealed record ApiBudgetInfo(
    int UsedInLast24h,
    int DailyBudget,
    int Remaining);

public sealed record MissingResultInfo(
    int MatchId,
    string Stage,
    DateTime KickoffAt,
    string? HomeTeam,
    string? AwayTeam,
    bool TeamsUnknown,
    DateTime ExpectedReadyAt,
    int PendingAttempts,
    bool Exhausted,
    DateTime? NextAttemptAt);

public sealed record UnresolvedFixtureInfo(
    int MatchId,
    string Stage,
    DateTime KickoffAt,
    string? HomePlaceholder,
    string? AwayPlaceholder,
    string Status,
    IReadOnlyList<int> WaitingForMatchIds);

public sealed record ForceFetchResult(
    int NewResults,
    int TeamFillsFromCompleted,
    int FixtureTeamFills,
    int CompletedDtosReceived,
    int RemainingUndetermined);
