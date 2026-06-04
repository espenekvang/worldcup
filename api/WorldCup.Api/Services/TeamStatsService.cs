using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using WorldCup.Api.DTOs;

namespace WorldCup.Api.Services;

/// <summary>
/// Henter lag-statistikk og innbyrdes oppgjør (H2H) for å gi tipperen
/// bakgrunnsinformasjon på matchdetalj-siden.
///
/// Datakilder, i prioritert rekkefølge:
/// 1. Ekstern fotball-API (hvis <see cref="IExternalTeamStatsClient"/> er
///    konfigurert til noe annet enn no-op). Cachet i minnet med TTL slik at
///    knockout-kamper plukker opp ferske tall så snart lagene er kjent.
/// 2. Lokal seed-fil <c>data/teamStats.json</c>. Komitteres i repoet og
///    fungerer uten ekstern avhengighet — fint for workshop/demo og som
///    fallback hvis ekstern API er nede.
///
/// Når lagkode mangler i begge kildene returneres <c>null</c>, og UI viser
/// "Statistikk ikke tilgjengelig" — viktig for sluttspillkamper der laget
/// ennå ikke er bestemt.
/// </summary>
public sealed class TeamStatsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IExternalTeamStatsClient _externalClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TeamStatsService> _logger;
    private readonly Lazy<SeedData> _seed;

    public TeamStatsService(
        IExternalTeamStatsClient externalClient,
        IMemoryCache cache,
        ILogger<TeamStatsService> logger,
        IWebHostEnvironment env)
    {
        _externalClient = externalClient;
        _cache = cache;
        _logger = logger;
        _seed = new Lazy<SeedData>(() => LoadSeed(env, logger), isThreadSafe: true);
    }

    public async Task<TeamStatsResponse?> GetTeamStatsAsync(string teamCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(teamCode)) return null;
        var key = teamCode.Trim().ToUpperInvariant();

        var cacheKey = $"team-stats:{key}";
        if (_cache.TryGetValue<TeamStatsResponse?>(cacheKey, out var cached))
        {
            return cached;
        }

        TeamStatsResponse? external = null;
        try
        {
            external = await _externalClient.GetTeamStatsAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Ekstern team-stats lookup feilet for {Team}", key);
        }

        var seeded = _seed.Value.Teams.TryGetValue(key, out var s) ? s : null;
        var result = Merge(external, seeded);

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<HeadToHeadResponse?> GetHeadToHeadAsync(string teamA, string teamB, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(teamA) || string.IsNullOrWhiteSpace(teamB)) return null;
        var a = teamA.Trim().ToUpperInvariant();
        var b = teamB.Trim().ToUpperInvariant();
        if (a == b) return null;

        // Vi cacher uavhengig av rekkefølge — det er samme oppgjør.
        var (canonA, canonB) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        var cacheKey = $"h2h:{canonA}-{canonB}";

        if (!_cache.TryGetValue<HeadToHeadResponse?>(cacheKey, out var stored))
        {
            stored = null;
            try
            {
                stored = await _externalClient.GetHeadToHeadAsync(canonA, canonB, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Ekstern H2H-lookup feilet for {A}-{B}", canonA, canonB);
            }

            stored ??= _seed.Value.HeadToHead.TryGetValue($"{canonA}-{canonB}", out var seeded) ? seeded : null;
            _cache.Set(cacheKey, stored, CacheTtl);
        }

        if (stored is null) return null;

        // Speilvend tellerne hvis klienten spurte i motsatt rekkefølge av canonical.
        if (stored.TeamA == a)
        {
            return stored;
        }

        return new HeadToHeadResponse(
            TeamA: a,
            TeamB: b,
            TotalMatches: stored.TotalMatches,
            TeamAWins: stored.TeamBWins,
            Draws: stored.Draws,
            TeamBWins: stored.TeamAWins,
            TeamAGoals: stored.TeamBGoals,
            TeamBGoals: stored.TeamAGoals,
            RecentMatches: stored.RecentMatches);
    }

    /// <summary>
    /// Slår sammen ekstern og seed-respons. Ekstern API gir typisk form,
    /// recent-matches og mål-snitt, mens seed har de "stabile" feltene
    /// (FIFA-rank, manager, nøkkelspiller, formasjon, forrige VM). Vi
    /// foretrekker eksterne verdier der de finnes, men faller tilbake til
    /// seed felt for felt. Returnerer null hvis begge er null.
    /// </summary>
    internal static TeamStatsResponse? Merge(TeamStatsResponse? external, TeamStatsResponse? seed)
    {
        if (external is null) return seed;
        if (seed is null) return external;

        return new TeamStatsResponse(
            TeamCode: external.TeamCode,
            FifaRank: external.FifaRank ?? seed.FifaRank,
            Manager: NullIfEmpty(external.Manager) ?? seed.Manager,
            StarPlayer: NullIfEmpty(external.StarPlayer) ?? seed.StarPlayer,
            PreferredFormation: NullIfEmpty(external.PreferredFormation) ?? seed.PreferredFormation,
            GoalsScoredAvg: external.GoalsScoredAvg ?? seed.GoalsScoredAvg,
            GoalsConcededAvg: external.GoalsConcededAvg ?? seed.GoalsConcededAvg,
            RecentForm: NullIfEmpty(external.RecentForm) ?? seed.RecentForm,
            RecentMatches: external.RecentMatches is { Count: > 0 } ? external.RecentMatches : seed.RecentMatches,
            KeyAbsences: external.KeyAbsences is { Count: > 0 } ? external.KeyAbsences : seed.KeyAbsences,
            LastWorldCupResult: NullIfEmpty(external.LastWorldCupResult) ?? seed.LastWorldCupResult,
            Squad: external.Squad is { Count: > 0 } ? external.Squad : seed.Squad ?? []);

        static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static SeedData LoadSeed(IWebHostEnvironment env, ILogger logger)
    {
        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, "Data", "teamStats.json"),
            Path.Combine(AppContext.BaseDirectory, "Data", "teamStats.json"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                var doc = JsonSerializer.Deserialize<SeedFile>(stream, JsonOptions);
                if (doc is null) continue;

                var teams = doc.Teams ?? new Dictionary<string, TeamStatsResponse>();
                var h2h = doc.HeadToHead ?? new Dictionary<string, HeadToHeadResponse>();
                logger.LogInformation("Lastet team-stats seed fra {Path}: {Teams} lag, {H2H} oppgjør", path, teams.Count, h2h.Count);
                return new SeedData(teams, h2h);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Klarte ikke parse team-stats seed fra {Path}", path);
            }
        }

        logger.LogWarning("Ingen team-stats seed funnet — alle oppslag returnerer null. Sjekk data/teamStats.json");
        return new SeedData(new Dictionary<string, TeamStatsResponse>(), new Dictionary<string, HeadToHeadResponse>());
    }

    private sealed record SeedData(
        IReadOnlyDictionary<string, TeamStatsResponse> Teams,
        IReadOnlyDictionary<string, HeadToHeadResponse> HeadToHead);

    private sealed class SeedFile
    {
        public Dictionary<string, TeamStatsResponse>? Teams { get; set; }

        [JsonPropertyName("headToHead")]
        public Dictionary<string, HeadToHeadResponse>? HeadToHead { get; set; }
    }
}

/// <summary>
/// Adapter for ekstern fotball-API (f.eks. football-data.org eller api-football).
/// Standard registrering er <see cref="NoopExternalTeamStatsClient"/> som alltid
/// returnerer null — da brukes seed-filen. Bytt registrering i Program.cs når
/// du har en API-nøkkel og en konkret implementasjon.
/// </summary>
public interface IExternalTeamStatsClient
{
    Task<TeamStatsResponse?> GetTeamStatsAsync(string teamCode, CancellationToken ct);
    Task<HeadToHeadResponse?> GetHeadToHeadAsync(string teamA, string teamB, CancellationToken ct);
}

public sealed class NoopExternalTeamStatsClient : IExternalTeamStatsClient
{
    public Task<TeamStatsResponse?> GetTeamStatsAsync(string teamCode, CancellationToken ct) =>
        Task.FromResult<TeamStatsResponse?>(null);

    public Task<HeadToHeadResponse?> GetHeadToHeadAsync(string teamA, string teamB, CancellationToken ct) =>
        Task.FromResult<HeadToHeadResponse?>(null);
}
