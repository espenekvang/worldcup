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

    public async Task<List<Wc2026MatchDto>> GetCompletedMatchesAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/matches?status=completed", ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("WC2026 API rate limit hit");
                return [];
            }

            response.EnsureSuccessStatusCode();

            var matches = await response.Content.ReadFromJsonAsync<List<Wc2026MatchDto>>(JsonOptions, ct);
            return matches ?? [];
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("WC2026 API request timed out");
            return [];
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("WC2026 API request timed out");
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WC2026 API request failed");
            return [];
        }
    }

    public int? MapToLocalMatchId(DateTime kickoffAt, MatchSchedule schedule) =>
        schedule.GetAllMatches()
            .FirstOrDefault(m => Math.Abs((m.Date - kickoffAt).TotalMinutes) <= TimeSpan.FromMinutes(60).TotalMinutes)
            ?.Id;
}

public sealed class Wc2026MatchDto
{
    public int MatchNumber { get; init; }
    public string Home { get; init; } = "";
    public string Away { get; init; } = "";
    public DateTime KickoffAt { get; init; }
    public Wc2026ScoreDto? Score { get; init; }
}

public sealed class Wc2026ScoreDto
{
    public int[]? Ft { get; init; }
}
