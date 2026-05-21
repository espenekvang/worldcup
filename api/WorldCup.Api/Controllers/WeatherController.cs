using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorldCup.Api.Services;

namespace WorldCup.Api.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController(WeatherService weatherService) : ControllerBase
{
    /// <summary>
    /// Henter daglig værvarsel for et stadion på en gitt dato (yyyy-MM-dd).
    /// Returnerer 204 No Content hvis vi ikke har koordinater for stadion eller
    /// hvis datoen er utenfor prognosehorisonten (~16 dager frem).
    /// </summary>
    [HttpGet("{venueId}/{date}")]
    [AllowAnonymous]
    public async Task<ActionResult<WeatherForecast>> Get(string venueId, string date, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return BadRequest("Date must be in yyyy-MM-dd format.");
        }

        var forecast = await weatherService.GetForecastAsync(venueId, parsed, ct);
        if (forecast is null)
        {
            return NoContent();
        }

        // Tillat at klienten (eller en mellomliggende cache) holder svaret en stund —
        // serveren cacher uansett, men dette sparer trafikk når flere kamper deler dato.
        Response.Headers.CacheControl = "public, max-age=1800";
        return Ok(forecast);
    }
}
