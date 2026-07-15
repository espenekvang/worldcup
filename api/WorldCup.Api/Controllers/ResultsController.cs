using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers;

[ApiController]
[Route("api/results")]
public class ResultsController(
    AppDbContext dbContext,
    ScoringService scoringService,
    MatchScheduleProvider scheduleProvider,
    LeagueStatsCalculator statsCalculator,
    KnockoutResolutionService knockoutResolution) : ControllerBase
{
    [HttpPut("/api/admin/results/{matchId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ResultResponse>> SetResult(
        int matchId,
        [FromBody] AdminSetResultRequest request,
        CancellationToken ct)
    {
        var match = scheduleProvider.Current.GetMatch(matchId);
        if (match is null)
        {
            return NotFound();
        }

        if (request.HomeScore < 0 || request.AwayScore < 0)
        {
            return BadRequest("Scores cannot be negative");
        }

        var existing = await dbContext.MatchResults
            .FirstOrDefaultAsync(r => r.MatchId == matchId, ct);

        var refereeName = string.IsNullOrWhiteSpace(request.Referee) ? null : request.Referee.Trim();

        if (existing is not null)
        {
            existing.HomeScore = request.HomeScore;
            existing.AwayScore = request.AwayScore;
            existing.FetchedAt = DateTime.UtcNow;
            if (refereeName is not null)
            {
                existing.Referee = refereeName;
            }
        }
        else
        {
            dbContext.MatchResults.Add(new Models.MatchResult
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                HomeScore = request.HomeScore,
                AwayScore = request.AwayScore,
                FetchedAt = DateTime.UtcNow,
                Referee = refereeName
            });
        }

        // Score ALL predictions for this match across ALL groups
        var predictions = await dbContext.Predictions
            .Where(p => p.MatchId == matchId)
            .ToListAsync(ct);

        foreach (var prediction in predictions)
        {
            prediction.Points = scoringService.CalculatePoints(
                prediction.HomeScore,
                prediction.AwayScore,
                request.HomeScore,
                request.AwayScore);
        }

        await dbContext.SaveChangesAsync(ct);

        // Forsøk å løse opp etterfølgende sluttspillkamper lokalt (f.eks. semifinalen når
        // begge kvartfinaler er avgjort). Uten dette avanserer bare den automatiske
        // oppstrøms-hentingen bracket-en, så et manuelt registrert resultat ville latt
        // «Vinner kamp N» stå uoppløst nedover i bracket-en.
        await knockoutResolution.ResolveAndPersistAsync(ct);

        return Ok(new ResultResponse
        {
            MatchId = matchId,
            HomeScore = request.HomeScore,
            AwayScore = request.AwayScore,
            FetchedAt = DateTime.UtcNow
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetResults()
    {
        var results = await dbContext.MatchResults
            .OrderBy(result => result.MatchId)
            .Select(result => new ResultResponse
            {
                MatchId = result.MatchId,
                HomeScore = result.HomeScore,
                AwayScore = result.AwayScore,
                FetchedAt = result.FetchedAt
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("points")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PointsResponse>>> GetPoints()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        // Bettinger er globale per bruker, så vi trenger ikke filtrere på liga her.
        var rawData = await (
            from result in dbContext.MatchResults
            join prediction in dbContext.Predictions on result.MatchId equals prediction.MatchId
            where prediction.UserId == userId.Value
            orderby result.MatchId
            select new
            {
                result.MatchId,
                PredictedHome = prediction.HomeScore,
                PredictedAway = prediction.AwayScore,
                ActualHome = result.HomeScore,
                ActualAway = result.AwayScore
            })
            .AsNoTracking()
            .ToListAsync();

        var points = rawData.Select(row => new PointsResponse
        {
            MatchId = row.MatchId,
            Points = scoringService.CalculatePoints(row.PredictedHome, row.PredictedAway, row.ActualHome, row.ActualAway),
            OutcomePoints = GetOutcomePoints(row.PredictedHome, row.PredictedAway, row.ActualHome, row.ActualAway),
            HomeGoalPoints = row.PredictedHome == row.ActualHome ? 1 : 0,
            AwayGoalPoints = row.PredictedAway == row.ActualAway ? 1 : 0
        }).ToList();

        return Ok(points);
    }

    [HttpGet("leaderboard")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<LeaderboardEntry>>> GetLeaderboard()
    {
        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        // Finn den siste kampen som har et registrert resultat (basert på FetchedAt).
        // Brukes for å beregne plassering FØR denne kampen, slik at frontend kan vise
        // pil opp/ned ved siden av poengsummen.
        var latestMatchId = await dbContext.MatchResults
            .OrderByDescending(r => r.FetchedAt)
            .Select(r => (int?)r.MatchId)
            .FirstOrDefaultAsync();

        var rawEntries = await dbContext.BettingGroupMembers
            .Where(m => m.BettingGroupId == groupId)
            .Select(m => new
            {
                m.UserId,
                Name = m.User.Name,
                Picture = m.User.Picture,
                m.HasPaid,
                TotalPoints = dbContext.Predictions
                    .Where(p => p.UserId == m.UserId && p.Points != null)
                    .Sum(p => (int?)p.Points) ?? 0,
                MatchCount = dbContext.Predictions
                    .Count(p => p.UserId == m.UserId && p.Points != null),
                LastMatchPoints = latestMatchId == null ? 0 : dbContext.Predictions
                    .Where(p => p.UserId == m.UserId
                                && p.MatchId == latestMatchId.Value
                                && p.Points != null)
                    .Select(p => (int?)p.Points)
                    .FirstOrDefault() ?? 0
            })
            .AsNoTracking()
            .ToListAsync();

        // Beregn forrige plassering ved å sortere på (TotalPoints - LastMatchPoints).
        // PreviousRank er kun meningsfull dersom det finnes en tidligere kamp; dvs.
        // det må eksistere minst én kamp med resultat utover den siste.
        var hasPreviousMatch = latestMatchId != null
            && await dbContext.MatchResults.CountAsync() > 1;

        Dictionary<Guid, int> previousRanks;
        if (hasPreviousMatch)
        {
            // Bruk "standard competition ranking" (1-2-2-4): spillere med lik
            // poengsum deler plassering, slik at det stemmer med plasseringen
            // som vises i klienten.
            var orderedPrev = rawEntries
                .Select(e => new { e.UserId, PrevPoints = e.TotalPoints - e.LastMatchPoints, e.Name })
                .OrderByDescending(e => e.PrevPoints)
                .ThenBy(e => e.Name)
                .ToList();

            previousRanks = new Dictionary<Guid, int>();
            int rank = 0;
            int? lastPoints = null;
            for (int idx = 0; idx < orderedPrev.Count; idx++)
            {
                var e = orderedPrev[idx];
                if (lastPoints == null || e.PrevPoints != lastPoints)
                {
                    rank = idx + 1;
                    lastPoints = e.PrevPoints;
                }
                previousRanks[e.UserId] = rank;
            }
        }
        else
        {
            previousRanks = new Dictionary<Guid, int>();
        }

        var leaderboard = rawEntries
            .OrderByDescending(e => e.TotalPoints)
            .ThenBy(e => e.Name)
            .Select(e => new LeaderboardEntry
            {
                Name = e.Name,
                Picture = e.Picture,
                TotalPoints = e.TotalPoints,
                MatchCount = e.MatchCount,
                PreviousRank = previousRanks.TryGetValue(e.UserId, out var r) ? r : (int?)null,
                HasPaid = e.HasPaid
            })
            .ToList();

        return Ok(leaderboard);
    }

    [HttpGet("leaderboard/global")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<GlobalLeaderboardEntry>>> GetGlobalLeaderboard()
    {
        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        // Hent alle brukere som har minst én scoret prediction
        var allUsers = await dbContext.Predictions
            .Where(p => p.Points != null)
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => (int?)p.Points) ?? 0,
                MatchCount = g.Count()
            })
            .ToListAsync();

        // Hent medlemmer av gjeldende gruppe
        var currentGroupMemberIds = await dbContext.BettingGroupMembers
            .Where(m => m.BettingGroupId == groupId)
            .Select(m => m.UserId)
            .ToListAsync();
        var currentGroupMemberSet = new HashSet<Guid>(currentGroupMemberIds);

        // For anonyme brukere: hent ett liganavn per bruker
        var userGroupNames = await dbContext.BettingGroupMembers
            .Where(m => !currentGroupMemberSet.Contains(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                GroupName = g.Select(m => m.BettingGroup.Name).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.UserId, x => x.GroupName);

        // Hent brukerinfo for alle brukere (navn + bilde)
        var userIds = allUsers.Select(u => u.UserId).ToList();
        var userInfos = await dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Picture })
            .ToDictionaryAsync(u => u.Id);

        var globalLeaderboard = allUsers
            .OrderByDescending(u => u.TotalPoints)
            .ThenBy(u => userInfos.GetValueOrDefault(u.UserId)?.Name ?? "")
            .Select(u =>
            {
                var isInGroup = currentGroupMemberSet.Contains(u.UserId);
                var info = userInfos.GetValueOrDefault(u.UserId);
                return new GlobalLeaderboardEntry
                {
                    Name = isInGroup ? info?.Name : null,
                    Picture = isInGroup ? info?.Picture : null,
                    TotalPoints = u.TotalPoints,
                    MatchCount = u.MatchCount,
                    IsInCurrentGroup = isInGroup,
                    GroupName = isInGroup ? null : userGroupNames.GetValueOrDefault(u.UserId)
                };
            })
            .ToList();

        return Ok(globalLeaderboard);
    }

    [HttpGet("stats")]
    [Authorize]
    public async Task<ActionResult<LeagueStatsResponse>> GetLeagueStats(CancellationToken ct)
    {
        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        var memberRows = await dbContext.BettingGroupMembers
            .Where(m => m.BettingGroupId == groupId)
            .Select(m => new { m.UserId, m.User.Name, m.User.Picture })
            .AsNoTracking()
            .ToListAsync(ct);
        var members = memberRows
            .Select(m => new StatMember(m.UserId, m.Name, m.Picture))
            .ToList();

        var memberIds = members.Select(m => m.UserId).ToList();

        var predictionRows = await dbContext.Predictions
            .Where(p => memberIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.MatchId, p.HomeScore, p.AwayScore })
            .AsNoTracking()
            .ToListAsync(ct);
        var predictions = predictionRows
            .Select(p => new StatPrediction(p.UserId, p.MatchId, p.HomeScore, p.AwayScore))
            .ToList();

        var resultRows = await dbContext.MatchResults
            .Select(r => new { r.MatchId, r.HomeScore, r.AwayScore, r.FetchedAt })
            .AsNoTracking()
            .ToListAsync(ct);
        var results = resultRows
            .Select(r => new StatResult(r.MatchId, r.HomeScore, r.AwayScore, r.FetchedAt))
            .ToList();

        var schedule = scheduleProvider.Current;
        var matchInfos = schedule.GetAllMatches()
            .Select(m => new StatMatchInfo(m.Id, m.Stage, m.Date, schedule.IsMatchLocked(m.Id)))
            .ToList();

        var stats = statsCalculator.Calculate(members, predictions, results, matchInfos);
        return Ok(stats);
    }

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<(Guid groupId, bool isValid)> ValidateGroupMembership()
    {
        var groupIdStr = Request.Headers["X-Group-Id"].FirstOrDefault();
        if (!Guid.TryParse(groupIdStr, out var groupId)) return (Guid.Empty, false);

        var userId = GetAuthenticatedUserId();
        if (userId is null) return (Guid.Empty, false);

        var isMember = await dbContext.BettingGroupMembers
            .AnyAsync(m => m.BettingGroupId == groupId && m.UserId == userId.Value);

        return (groupId, isMember);
    }

    private static int GetOutcomePoints(int predictedHome, int predictedAway, int actualHome, int actualAway)
    {
        var predictedOutcome = GetOutcome(predictedHome, predictedAway);
        var actualOutcome = GetOutcome(actualHome, actualAway);

        return predictedOutcome == actualOutcome ? 2 : 0;
    }

    private static int GetOutcome(int homeGoals, int awayGoals)
    {
        if (homeGoals > awayGoals)
        {
            return 1;
        }

        if (homeGoals < awayGoals)
        {
            return -1;
        }

        return 0;
    }
}

public sealed record AdminSetResultRequest(int HomeScore, int AwayScore, string? Referee = null);
