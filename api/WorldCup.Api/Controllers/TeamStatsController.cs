using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorldCup.Api.DTOs;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers;

[ApiController]
[Route("api/team-stats")]
public class TeamStatsController(TeamStatsService statsService) : ControllerBase
{
    /// <summary>
    /// Henter form, snittall, manager, nøkkelspiller og siste kamper for ett lag.
    /// Returnerer 404 hvis vi ikke har data for laget (typisk hvis lagkoden ikke
    /// er bestemt ennå, f.eks. kvartfinale før gruppespillet er ferdig).
    /// </summary>
    [HttpGet("{teamCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeamStatsResponse>> Get(string teamCode, CancellationToken ct)
    {
        var stats = await statsService.GetTeamStatsAsync(teamCode, ct);
        if (stats is null) return NotFound();

        // Mest stats endrer seg sjelden — la klienter cache i 30 min.
        Response.Headers.CacheControl = "public, max-age=1800";
        return Ok(stats);
    }

    /// <summary>
    /// Henter innbyrdes oppgjør (head-to-head) for to lag. Returnerer 404 hvis
    /// vi ikke har data for paret.
    /// </summary>
    [HttpGet("h2h/{teamA}/{teamB}")]
    [AllowAnonymous]
    public async Task<ActionResult<HeadToHeadResponse>> Head2Head(string teamA, string teamB, CancellationToken ct)
    {
        var h2h = await statsService.GetHeadToHeadAsync(teamA, teamB, ct);
        if (h2h is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=1800";
        return Ok(h2h);
    }
}
