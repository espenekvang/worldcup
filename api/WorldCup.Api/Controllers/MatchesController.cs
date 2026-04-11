using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController(
    MatchScheduleProvider scheduleProvider,
    TeamCodeMapper teamCodeMapper,
    MatchFileWriter matchFileWriter) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<IEnumerable<MatchResponse>> GetMatches()
    {
        var matches = scheduleProvider.Current.GetAllMatches()
            .Select(match => new MatchResponse(
                match.Id,
                match.Date,
                match.HomeTeam,
                match.AwayTeam,
                match.HomePlaceholder,
                match.AwayPlaceholder,
                match.Group,
                match.Stage,
                match.VenueId))
            .ToList();

        return Ok(matches);
    }

    [HttpPut("/api/admin/matches/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MatchResponse>> OverrideMatch(
        int id,
        [FromBody] AdminMatchOverrideRequest request,
        CancellationToken ct)
    {
        var match = scheduleProvider.Current.GetMatch(id);
        if (match is null)
        {
            return NotFound();
        }

        if (string.Equals(match.Stage, "group", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Cannot override group stage matches");
        }

        if (request.HomeTeam is not null && !teamCodeMapper.IsValidCode(request.HomeTeam))
        {
            return BadRequest($"Invalid team code: {request.HomeTeam}");
        }

        if (request.AwayTeam is not null && !teamCodeMapper.IsValidCode(request.AwayTeam))
        {
            return BadRequest($"Invalid team code: {request.AwayTeam}");
        }

        var updatedMatch = new MatchEntry
        {
            Id = match.Id,
            Date = match.Date,
            Stage = match.Stage,
            HomeTeam = request.HomeTeam ?? match.HomeTeam,
            AwayTeam = request.AwayTeam ?? match.AwayTeam,
            HomePlaceholder = match.HomePlaceholder,
            AwayPlaceholder = match.AwayPlaceholder,
            Group = match.Group,
            VenueId = match.VenueId,
            ManualOverride = true,
        };

        var updatedMatches = scheduleProvider.Current.GetAllMatches()
            .Select(m => m.Id == id ? updatedMatch : m)
            .ToList();

        await matchFileWriter.WriteAsync(updatedMatches, ct);

        return Ok(new MatchResponse(
            updatedMatch.Id,
            updatedMatch.Date,
            updatedMatch.HomeTeam,
            updatedMatch.AwayTeam,
            updatedMatch.HomePlaceholder,
            updatedMatch.AwayPlaceholder,
            updatedMatch.Group,
            updatedMatch.Stage,
            updatedMatch.VenueId));
    }
}

public sealed record MatchResponse(
    int Id,
    DateTime Date,
    string? HomeTeam,
    string? AwayTeam,
    string? HomePlaceholder,
    string? AwayPlaceholder,
    string? Group,
    string Stage,
    string VenueId
);

public sealed record AdminMatchOverrideRequest(string? HomeTeam, string? AwayTeam);
