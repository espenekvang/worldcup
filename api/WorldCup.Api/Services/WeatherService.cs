using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace WorldCup.Api.Services;

/// <summary>
/// Henter daglig værvarsel fra Open-Meteo (gratis, ingen nøkkel) og cacher i minnet
/// per stadion+dato. Open-Meteo har ~16 dagers prognose — for kamper lenger frem i tid
/// returneres <c>null</c>.
/// </summary>
public sealed class WeatherService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(3);
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromMinutes(30);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, IMemoryCache cache, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.open-meteo.com/");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }
    }

    /// <summary>Koordinater (lat, lon) for VM 2026-stadioner. Nøkkel = <c>Venue.id</c>.</summary>
    private static readonly IReadOnlyDictionary<string, (double Lat, double Lon)> Coordinates =
        new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
        {
            ["azteca"]       = (19.3029, -99.1505),
            ["akron"]        = (20.6817, -103.4625),
            ["bbva"]         = (25.6692, -100.2447),
            ["bmo"]          = (43.6332, -79.4185),
            ["bcplace"]      = (49.2768, -123.1119),
            ["metlife"]      = (40.8136, -74.0744),
            ["rosebowl"]     = (34.1613, -118.1676),
            ["att"]          = (32.7473, -97.0945),
            ["sofi"]         = (33.9535, -118.3392),
            ["hardrock"]     = (25.9580, -80.2389),
            ["lincoln"]      = (39.9008, -75.1675),
            ["lumen"]        = (47.5952, -122.3316),
            ["nrg"]          = (29.6847, -95.4107),
            ["mercedesbenz"] = (33.7553, -84.4006),
            ["arrowhead"]    = (39.0489, -94.4839),
            ["levis"]        = (37.4030, -121.9690),
            ["gillette"]     = (42.0909, -71.2643),
        };

    public async Task<WeatherForecast?> GetForecastAsync(string venueId, DateOnly date, CancellationToken ct)
    {
        if (!Coordinates.TryGetValue(venueId, out var coords))
        {
            return null;
        }

        // Open-Meteo gir ~16 dager fremover. Skip kall som garantert ikke har data.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysAhead = date.DayNumber - today.DayNumber;
        if (daysAhead < -1 || daysAhead > 16)
        {
            return null;
        }

        var cacheKey = $"weather:{venueId}:{date:yyyy-MM-dd}";
        if (_cache.TryGetValue<WeatherForecast?>(cacheKey, out var cached))
        {
            return cached;
        }

        WeatherForecast? forecast = null;
        try
        {
            var iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var url = $"v1/forecast?latitude={coords.Lat.ToString(CultureInfo.InvariantCulture)}"
                + $"&longitude={coords.Lon.ToString(CultureInfo.InvariantCulture)}"
                + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max"
                + "&timezone=auto"
                + $"&start_date={iso}&end_date={iso}";

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open-Meteo returned {Status} for venue {VenueId} date {Date}", response.StatusCode, venueId, iso);
                _cache.Set<WeatherForecast?>(cacheKey, null, NotFoundTtl);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(cancellationToken: ct);
            if (payload?.Daily is null || payload.Daily.Time is null || payload.Daily.Time.Count == 0)
            {
                _cache.Set<WeatherForecast?>(cacheKey, null, NotFoundTtl);
                return null;
            }

            forecast = new WeatherForecast(
                Date: payload.Daily.Time[0],
                WeatherCode: payload.Daily.WeatherCode?[0] ?? 0,
                TempMaxC: payload.Daily.TemperatureMax?[0],
                TempMinC: payload.Daily.TemperatureMin?[0],
                PrecipitationMm: payload.Daily.PrecipitationSum?[0],
                PrecipitationProbabilityPct: payload.Daily.PrecipitationProbabilityMax?[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Klarte ikke hente værvarsel for {VenueId} {Date}", venueId, date);
            _cache.Set<WeatherForecast?>(cacheKey, null, NotFoundTtl);
            return null;
        }

        _cache.Set(cacheKey, forecast, CacheTtl);
        return forecast;
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("daily")]
        public OpenMeteoDaily? Daily { get; set; }
    }

    private sealed class OpenMeteoDaily
    {
        [JsonPropertyName("time")]
        public List<string>? Time { get; set; }

        [JsonPropertyName("weather_code")]
        public List<int>? WeatherCode { get; set; }

        [JsonPropertyName("temperature_2m_max")]
        public List<double?>? TemperatureMax { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public List<double?>? TemperatureMin { get; set; }

        [JsonPropertyName("precipitation_sum")]
        public List<double?>? PrecipitationSum { get; set; }

        [JsonPropertyName("precipitation_probability_max")]
        public List<int?>? PrecipitationProbabilityMax { get; set; }
    }
}

public sealed record WeatherForecast(
    string Date,
    int WeatherCode,
    double? TempMaxC,
    double? TempMinC,
    double? PrecipitationMm,
    int? PrecipitationProbabilityPct);
