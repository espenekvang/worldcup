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
public class ResultsController(AppDbContext dbContext, ScoringService scoringService) : ControllerBase
{
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
        if (userId is null)
        {
            return Unauthorized();
        }

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
        var leaderboard = await dbContext.Users
            .Select(u => new LeaderboardEntry
            {
                Name = u.Name,
                Picture = u.Picture,
                TotalPoints = dbContext.Predictions
                    .Where(p => p.UserId == u.Id && p.Points != null)
                    .Sum(p => (int?)p.Points) ?? 0,
                MatchCount = dbContext.Predictions
                    .Count(p => p.UserId == u.Id && p.Points != null)
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
