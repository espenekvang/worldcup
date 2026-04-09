using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/predictions")]
public class PredictionsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PredictionResponse>>> GetPredictions()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var predictions = await dbContext.Predictions
            .Where(prediction => prediction.UserId == userId.Value)
            .OrderBy(prediction => prediction.MatchId)
            .Select(prediction => new PredictionResponse
            {
                MatchId = prediction.MatchId,
                HomeScore = prediction.HomeScore,
                AwayScore = prediction.AwayScore,
                UpdatedAt = prediction.UpdatedAt
            })
            .ToListAsync();

        return Ok(predictions);
    }

    [HttpPut("{matchId:int}")]
    public async Task<ActionResult<PredictionResponse>> UpsertPrediction(int matchId, [FromBody] PredictionDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (matchId <= 0 || request.MatchId <= 0 || request.HomeScore < 0 || request.AwayScore < 0)
        {
            return BadRequest("MatchId must be greater than 0 and scores must be non-negative.");
        }

        if (request.MatchId != matchId)
        {
            return BadRequest("Route matchId must match request MatchId.");
        }

        var prediction = await dbContext.Predictions
            .SingleOrDefaultAsync(existingPrediction =>
                existingPrediction.UserId == userId.Value && existingPrediction.MatchId == matchId);

        var now = DateTime.UtcNow;
        var isNewPrediction = prediction is null;

        if (prediction is null)
        {
            prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                MatchId = matchId,
                HomeScore = request.HomeScore,
                AwayScore = request.AwayScore,
                UpdatedAt = now
            };

            dbContext.Predictions.Add(prediction);
        }
        else
        {
            prediction.HomeScore = request.HomeScore;
            prediction.AwayScore = request.AwayScore;
            prediction.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync();

        var response = new PredictionResponse
        {
            MatchId = prediction.MatchId,
            HomeScore = prediction.HomeScore,
            AwayScore = prediction.AwayScore,
            UpdatedAt = prediction.UpdatedAt
        };

        return isNewPrediction ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
    }

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
