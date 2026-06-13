using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WorldCup.Api.Services;

public sealed class Wc2026ApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<Wc2026ApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient = ConfigureClient(httpClient, configuration, logger);

    private static HttpClient ConfigureClient(HttpClient client, IConfiguration configuration, ILogger<Wc2026ApiClient> logger)
    {
        var baseUrl = configuration["Wc2026Api:BaseUrl"];
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            client.BaseAddress = baseUri;
        }
        else
        {
            logger.LogWarning("WC2026 API base URL is not configured correctly.");
        }

        var apiKey = configuration["Wc2026Api:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        else
        {
            logger.LogWarning("WC2026 API key is not configured.");
        }

        return client;
    }

    public Task<List<Wc2026MatchDto>> GetCompletedMatchesAsync(CancellationToken ct = default) =>
        GetMatchesAsync("/matches?status=completed", ct);

    public Task<List<Wc2026MatchDto>> GetScheduledMatchesAsync(CancellationToken ct = default) =>
        GetMatchesAsync("/matches?status=scheduled", ct);

    // Max characters of the raw response body we echo into logs. Enough to see the
    // JSON shape / an error payload without flooding the log sink.
    private const int MaxBodySnippet = 1000;

    private async Task<List<Wc2026MatchDto>> GetMatchesAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning(
                    "WC2026 API rate limit hit (429) for {Endpoint}. Retry-After: {RetryAfter}.",
                    endpoint,
                    response.Headers.RetryAfter?.ToString() ?? "n/a");
                return [];
            }

            // Read the body as a string first so we can log a snippet on any failure,
            // including non-success status codes and unparseable payloads.
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "WC2026 API call to {Endpoint} failed: {StatusCode} {Reason}. Body: {Body}",
                    endpoint,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    Snippet(body));
                return [];
            }

            List<Wc2026MatchDto>? matches;
            try
            {
                matches = JsonSerializer.Deserialize<List<Wc2026MatchDto>>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogError(
                    ex,
                    "WC2026 API call to {Endpoint} returned {StatusCode} but the body could not be parsed as a match list. Body: {Body}",
                    endpoint,
                    (int)response.StatusCode,
                    Snippet(body));
                return [];
            }

            if (matches is null || matches.Count == 0)
            {
                logger.LogWarning(
                    "WC2026 API call to {Endpoint} returned {StatusCode} with 0 matches. Body: {Body}",
                    endpoint,
                    (int)response.StatusCode,
                    Snippet(body));
                return [];
            }

            var withScore = matches.Count(m => m.Score?.Ft is [_, _]);
            logger.LogInformation(
                "WC2026 API {Endpoint} -> {StatusCode}: {Count} matches ({WithScore} with full-time score). First: {Sample}",
                endpoint,
                (int)response.StatusCode,
                matches.Count,
                withScore,
                DescribeSample(matches[0]));
            return matches;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("WC2026 API request to {Endpoint} timed out", endpoint);
            return [];
        }
        catch (OperationCanceledException)
        {
            // Genuine shutdown cancellation — not a fetch failure.
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WC2026 API request to {Endpoint} failed", endpoint);
            return [];
        }
    }

    private static string Snippet(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "<empty>";
        body = body.Trim();
        return body.Length <= MaxBodySnippet ? body : body[..MaxBodySnippet] + "…(truncated)";
    }

    /// <summary>
    /// Renders the fields we depend on for the first returned match so a field-name /
    /// shape mismatch is obvious in the logs: an unparsed <c>kickoffAt</c> shows up as
    /// <c>0001-01-01</c>, and a renamed/reshaped score shows up as <c>score.ft=null</c>.
    /// </summary>
    private static string DescribeSample(Wc2026MatchDto match)
    {
        var ft = match.Score?.Ft is { } values ? "[" + string.Join(",", values) + "]" : "null";
        return $"matchNumber={match.MatchNumber}, kickoffAt={match.KickoffAt:o}, score.ft={ft}";
    }

    public int? MapToLocalMatchId(DateTime kickoffAt, MatchSchedule schedule) =>
        schedule.GetAllMatches()
            .FirstOrDefault(m => Math.Abs((m.Date - kickoffAt).TotalMinutes) <= TimeSpan.FromMinutes(60).TotalMinutes)
            ?.Id;

    public int? MapToLocalMatchIdByMatchNumber(int matchNumber, MatchSchedule schedule) =>
        schedule.GetMatch(matchNumber)?.Id;
}

public sealed class Wc2026MatchDto
{
    public int MatchNumber { get; init; }
    public string Home { get; init; } = "";
    public string Away { get; init; } = "";
    public DateTime KickoffAt { get; init; }
    public Wc2026ScoreDto? Score { get; init; }

    /// <summary>
    /// Dommer for kampen, hvis oppstr\u00f8ms-API returnerer det. Feltet er valgfritt
    /// \u2014 hvis JSON ikke inneholder "referee" forblir det null og koden faller tilbake
    /// til standardnavnet "Dommeren".
    /// </summary>
    public string? Referee { get; init; }
}

public sealed class Wc2026ScoreDto
{
    public int[]? Ft { get; init; }
}
