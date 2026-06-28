using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

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

            var withScore = matches.Count(m => m.HomeScore is not null && m.AwayScore is not null);
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
    /// shape mismatch is obvious in the logs: an unparsed <c>kickoff_utc</c> shows up as
    /// <c>0001-01-01</c>, and a renamed/reshaped score shows up as <c>score=null</c>.
    /// </summary>
    private static string DescribeSample(Wc2026MatchDto match)
    {
        var score = match is { HomeScore: { } home, AwayScore: { } away } ? $"{home}-{away}" : "null";
        return $"matchNumber={match.MatchNumber}, kickoffAt={match.KickoffAt:o}, score={score}";
    }

    // How far a DTO kickoff may sit from a local fixture's kickoff before we refuse
    // to treat them as the same game (used only by the time-based fallback below).
    private const double KickoffMatchToleranceMinutes = 60;

    /// <summary>
    /// Resolves a completed-match DTO to a local fixture Id.
    /// <para>
    /// The primary lookup is by <paramref name="matchNumber"/> against the local schedule. When
    /// a match is found, the kickoff time is cross-validated: if it falls outside the tolerance
    /// window the number match is discarded and the time-based fallback is used instead. This
    /// guards against the API's match_number diverging from our local IDs in the knockout rounds
    /// (confirmed: the API's knockout match_numbers do not align 1:1 with our local IDs 73–104).
    /// </para>
    /// <para>
    /// The time-based fallback normalises both sides to UTC and resolves only when exactly one
    /// local fixture is within range — simultaneous kickoffs (final-round group games) must never
    /// be guessed by time alone.
    /// </para>
    /// </summary>
    public int? MapToLocalMatchId(int matchNumber, DateTime kickoffAt, MatchSchedule schedule)
    {
        var kickoffUtc = ToUtc(kickoffAt);

        if (schedule.GetMatch(matchNumber) is { } byNumber)
        {
            var delta = Math.Abs((ToUtc(byNumber.Date) - kickoffUtc).TotalMinutes);
            if (delta <= KickoffMatchToleranceMinutes)
            {
                return byNumber.Id;
            }

            // Match number resolves a local fixture but the kickoff doesn't align — the API's
            // numbering has diverged from ours for this stage. Fall through to the time lookup.
            logger.LogWarning(
                "match_number {MatchNumber} resolves to local fixture {LocalId} (kickoff {LocalKickoff:o}) " +
                "but API kickoff {ApiKickoff:o} is {DeltaMin:F0} min away — treating as numbering mismatch, falling back to kickoff lookup.",
                matchNumber, byNumber.Id, byNumber.Date, kickoffAt, delta);
        }

        var candidates = schedule.GetAllMatches()
            .Where(m => Math.Abs((ToUtc(m.Date) - kickoffUtc).TotalMinutes) <= KickoffMatchToleranceMinutes)
            .ToList();

        return candidates.Count == 1 ? candidates[0].Id : null;
    }

    /// <summary>
    /// Resolves a scheduled-match DTO to a local fixture Id by match number, cross-validating
    /// with kickoff time to guard against knockout-stage numbering mismatches.
    /// </summary>
    public int? MapToLocalMatchIdByMatchNumber(int matchNumber, DateTime kickoffAt, MatchSchedule schedule)
    {
        if (schedule.GetMatch(matchNumber) is { } byNumber)
        {
            var delta = Math.Abs((ToUtc(byNumber.Date) - ToUtc(kickoffAt)).TotalMinutes);
            if (delta <= KickoffMatchToleranceMinutes)
            {
                return byNumber.Id;
            }

            logger.LogWarning(
                "match_number {MatchNumber} resolves to local fixture {LocalId} but kickoff delta is {DeltaMin:F0} min — using kickoff fallback.",
                matchNumber, byNumber.Id, delta);
        }

        var kickoffUtc = ToUtc(kickoffAt);
        var candidates = schedule.GetAllMatches()
            .Where(m => Math.Abs((ToUtc(m.Date) - kickoffUtc).TotalMinutes) <= KickoffMatchToleranceMinutes)
            .ToList();

        return candidates.Count == 1 ? candidates[0].Id : null;
    }

    /// <summary>
    /// Normalizes a <see cref="DateTime"/> to UTC for instant-comparison. An offset in the
    /// source JSON yields <see cref="DateTimeKind.Local"/> (converted to host local) and is
    /// converted back; a <c>Z</c> yields <see cref="DateTimeKind.Utc"/> as-is; an offset-less
    /// value is treated as UTC to match how matches.json is authored.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

/// <summary>
/// Mirrors the WC2026 API match payload, which is flat snake_case (e.g.
/// <c>match_number</c>, <c>home_team</c>, <c>home_score</c>). Property names are bound
/// explicitly with <see cref="JsonPropertyNameAttribute"/> so binding does not depend on
/// the serializer's naming policy.
/// </summary>
public sealed class Wc2026MatchDto
{
    /// <summary>FIFA match number (1..104), equals the local <see cref="MatchEntry.Id"/>.</summary>
    [JsonPropertyName("match_number")]
    public int MatchNumber { get; init; }

    [JsonPropertyName("home_team")]
    public string Home { get; init; } = "";

    [JsonPropertyName("away_team")]
    public string Away { get; init; } = "";

    /// <summary>Three-letter team code (e.g. "MEX"); aligns with our teams.json keys.</summary>
    [JsonPropertyName("home_team_code")]
    public string? HomeCode { get; init; }

    [JsonPropertyName("away_team_code")]
    public string? AwayCode { get; init; }

    [JsonPropertyName("kickoff_utc")]
    public DateTime KickoffAt { get; init; }

    /// <summary>
    /// Final score in play. For knockout games decided in extra time this already includes
    /// the ET goals; a penalty shootout is reported separately in <see cref="HomePen"/>/
    /// <see cref="AwayPen"/> and is not counted into the score.
    /// </summary>
    [JsonPropertyName("home_score")]
    public int? HomeScore { get; init; }

    [JsonPropertyName("away_score")]
    public int? AwayScore { get; init; }

    /// <summary>Penalty-shootout tally, null outside knockout / for games not decided on penalties.</summary>
    [JsonPropertyName("home_pen")]
    public int? HomePen { get; init; }

    [JsonPropertyName("away_pen")]
    public int? AwayPen { get; init; }

    /// <summary>Match phase: PRE, 1H, HT, 2H, ET1, ET2, PEN, FT, or FT_PEN.</summary>
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    /// <summary>
    /// Dommer for kampen, hvis oppstr\u00f8ms-API returnerer det. Feltet er valgfritt
    /// \u2014 hvis JSON ikke inneholder "referee" forblir det null og koden faller tilbake
    /// til standardnavnet "Dommeren".
    /// </summary>
    [JsonPropertyName("referee")]
    public string? Referee { get; init; }
}
