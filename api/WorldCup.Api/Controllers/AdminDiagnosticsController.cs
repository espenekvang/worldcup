using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminDiagnosticsController(
    AppDbContext dbContext,
    MatchScheduleProvider scheduleProvider) : ControllerBase
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
    /// stored result, and which knockout fixtures still have null teams together with what they're
    /// waiting for. Intended for admin-only use when the automatic fetcher appears to have missed
    /// results — the response makes it easy to target manual overrides.
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

        // Matches that are due (teams known, buffer elapsed) but have no result yet.
        var missingResults = new List<MissingResultInfo>();
        foreach (var match in allMatches.OrderBy(m => m.Date))
        {
            if (existingResultMatchIds.Contains(match.Id)) continue;
            if (match.AreTeamsUndetermined) continue;

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
