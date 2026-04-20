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
    MatchScheduleProvider scheduleProvider) : ControllerBase
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

        if (existing is not null)
        {
            existing.HomeScore = request.HomeScore;
            existing.AwayScore = request.AwayScore;
            existing.FetchedAt = DateTime.UtcNow;
        }
        else
        {
            dbContext.MatchResults.Add(new Models.MatchResult
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                HomeScore = request.HomeScore,
                AwayScore = request.AwayScore,
                FetchedAt = DateTime.UtcNow
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

        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        var rawData = await (
            from result in dbContext.MatchResults
            join prediction in dbContext.Predictions on result.MatchId equals prediction.MatchId
            where prediction.UserId == userId.Value && prediction.BettingGroupId == groupId
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

        var leaderboard = await dbContext.BettingGroupMembers
            .Where(m => m.BettingGroupId == groupId)
            .Select(m => new LeaderboardEntry
            {
                Name = m.User.Name,
                Picture = m.User.Picture,
                TotalPoints = dbContext.Predictions
                    .Where(p => p.UserId == m.UserId && p.BettingGroupId == groupId && p.Points != null)
                    .Sum(p => (int?)p.Points) ?? 0,
                MatchCount = dbContext.Predictions
                    .Count(p => p.UserId == m.UserId && p.BettingGroupId == groupId && p.Points != null)
            })
            .OrderByDescending(e => e.TotalPoints)
            .ThenBy(e => e.Name)
            .AsNoTracking()
            .ToListAsync();

        return Ok(leaderboard);
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

public sealed record AdminSetResultRequest(int HomeScore, int AwayScore);
