using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WorldCup.Api.Data;
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
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly MatchScheduleProvider? _scheduleProvider;

    public TeamStatsService(
        IExternalTeamStatsClient externalClient,
        IMemoryCache cache,
        ILogger<TeamStatsService> logger,
        IWebHostEnvironment env,
        IServiceScopeFactory? scopeFactory = null,
        MatchScheduleProvider? scheduleProvider = null)
    {
        _externalClient = externalClient;
        _cache = cache;
        _logger = logger;
        _seed = new Lazy<SeedData>(() => LoadSeed(env, logger), isThreadSafe: true);
        // Valgfrie avhengigheter — settes i prod (DI), men kan utelates i enhetstester
        // som bare verifiserer seed/merge-logikken. Når de er null hoppes VM-overlayet over.
        _scopeFactory = scopeFactory;
        _scheduleProvider = scheduleProvider;
    }

    public async Task<TeamStatsResponse?> GetTeamStatsAsync(string teamCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(teamCode)) return null;
        var key = teamCode.Trim().ToUpperInvariant();

        var baseStats = await GetBaseStatsAsync(key, ct);

        // Legg de faktiske VM-kampene oppå seed/ekstern-formen slik at «Form siste 5
        // kamper» fylles ut fortløpende etter hvert som resultater registreres. Disse
        // hentes ferskt per kall (utenfor base-cachen) så et nytt resultat slår igjennom
        // med en gang i stedet for å vente på at cachen utløper.
        var worldCupMatches = await GetWorldCupMatchesAsync(key, ct);
        return WithWorldCupForm(baseStats, key, worldCupMatches);
    }

    /// <summary>
    /// Henter «stabil» lag-statistikk (ekstern API + seed-fil) og cacher den. Dette er
    /// dataen som sjelden endrer seg (FIFA-rank, manager, formasjon osv.) — de ferske
    /// VM-kampene legges på etterpå i <see cref="GetTeamStatsAsync"/>.
    /// </summary>
    private async Task<TeamStatsResponse?> GetBaseStatsAsync(string key, CancellationToken ct)
    {
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

    /// <summary>
    /// Bygger en liste over VM-kamper laget har spilt (dvs. har et registrert resultat),
    /// nyeste først. Kobler kampoppsettet (<see cref="MatchScheduleProvider"/>) mot
    /// resultat-tabellen i databasen. Returnerer tom liste hvis avhengighetene ikke er
    /// satt (enhetstester) eller laget ikke har noen spilte kamper ennå.
    /// </summary>
    private async Task<IReadOnlyList<RecentMatchEntry>> GetWorldCupMatchesAsync(string teamCode, CancellationToken ct)
    {
        if (_scopeFactory is null || _scheduleProvider is null)
        {
            return Array.Empty<RecentMatchEntry>();
        }

        var teamMatches = _scheduleProvider.Current.GetAllMatches()
            .Where(m =>
                string.Equals(m.HomeTeam, teamCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.AwayTeam, teamCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (teamMatches.Count == 0)
        {
            return Array.Empty<RecentMatchEntry>();
        }

        var matchIds = teamMatches.Select(m => m.Id).ToHashSet();

        Dictionary<int, Models.MatchResult> resultsById;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var results = await db.MatchResults
                .Where(r => matchIds.Contains(r.MatchId))
                .AsNoTracking()
                .ToListAsync(ct);
            resultsById = results.ToDictionary(r => r.MatchId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Kunne ikke hente VM-resultater for form-overlay ({Team})", teamCode);
            return Array.Empty<RecentMatchEntry>();
        }

        var entries = new List<(DateTime Date, RecentMatchEntry Entry)>();
        foreach (var match in teamMatches)
        {
            if (!resultsById.TryGetValue(match.Id, out var result)) continue;

            var isHome = string.Equals(match.HomeTeam, teamCode, StringComparison.OrdinalIgnoreCase);
            var opponent = isHome ? match.AwayTeam : match.HomeTeam;
            if (string.IsNullOrWhiteSpace(opponent)) continue;

            var goalsFor = isHome ? result.HomeScore : result.AwayScore;
            var goalsAgainst = isHome ? result.AwayScore : result.HomeScore;
            var outcome = goalsFor > goalsAgainst ? "W" : goalsFor < goalsAgainst ? "L" : "D";

            entries.Add((match.Date, new RecentMatchEntry(
                Date: match.Date.ToString("yyyy-MM-dd"),
                Opponent: opponent.ToUpperInvariant(),
                Venue: isHome ? "home" : "away",
                GoalsFor: goalsFor,
                GoalsAgainst: goalsAgainst,
                Result: outcome,
                Competition: "VM 2026")));
        }

        return entries
            .OrderByDescending(e => e.Date)
            .Select(e => e.Entry)
            .ToList();
    }

    /// <summary>
    /// Slår sammen spilte VM-kamper med eksisterende seed/ekstern-form. VM-kampene legges
    /// først (nyeste først), og form-strengen + mål-snittene regnes på nytt fra den
    /// kombinerte lista så «Form siste 5 kamper» og «Sammenligning» gjenspeiler det som
    /// faktisk har skjedd i mesterskapet. Uten VM-kamper returneres base uendret.
    /// </summary>
    public static TeamStatsResponse? WithWorldCupForm(
        TeamStatsResponse? baseStats,
        string teamCode,
        IReadOnlyList<RecentMatchEntry> worldCupMatches)
    {
        if (worldCupMatches.Count == 0) return baseStats;

        var seedMatches = baseStats?.RecentMatches ?? Array.Empty<RecentMatchEntry>();
        var combined = worldCupMatches.Concat(seedMatches).Take(10).ToList();

        // recentForm: siste 5 kamper, eldste først (matcher DTO-konvensjonen "WWDWL").
        var recentForm = string.Concat(combined.Take(5).Reverse().Select(m => m.Result));

        var goalsScoredAvg = Math.Round(combined.Average(m => (double)m.GoalsFor), 2);
        var goalsConcededAvg = Math.Round(combined.Average(m => (double)m.GoalsAgainst), 2);

        if (baseStats is null)
        {
            return new TeamStatsResponse(
                TeamCode: teamCode,
                FifaRank: null,
                Manager: null,
                StarPlayer: null,
                PreferredFormation: null,
                GoalsScoredAvg: goalsScoredAvg,
                GoalsConcededAvg: goalsConcededAvg,
                RecentForm: recentForm,
                RecentMatches: combined,
                KeyAbsences: Array.Empty<string>(),
                LastWorldCupResult: null);
        }

        return baseStats with
        {
            RecentForm = recentForm,
            RecentMatches = combined,
            GoalsScoredAvg = goalsScoredAvg,
            GoalsConcededAvg = goalsConcededAvg,
        };
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

        // Orienter base-dataen i klientens rekkefølge (teamA = a) før vi legger på VM-kamper.
        // Speilvend tellerne hvis klienten spurte i motsatt rekkefølge av canonical.
        var baseH2h = stored is null || stored.TeamA == a ? stored : Mirror(stored, a, b);

        // Legg de faktiske VM-kampene mellom lagene oppå seed/ekstern-historikken — på samme
        // måte som «Form i VM» overlayes i GetTeamStatsAsync. Hentes ferskt per kall (utenfor
        // base-cachen) så et nytt resultat slår igjennom umiddelbart. Hvis vi verken har base
        // eller spilte VM-kamper returneres null, og UI viser «ingen tidligere oppgjør».
        var worldCupMatches = await GetWorldCupHeadToHeadMatchesAsync(a, b, ct);
        return WithWorldCupHeadToHead(baseH2h, a, b, worldCupMatches);

        // Speilvender en lagret H2H slik at TeamA blir det laget klienten spurte om først.
        static HeadToHeadResponse Mirror(HeadToHeadResponse src, string clientA, string clientB) =>
            new(
                TeamA: clientA,
                TeamB: clientB,
                TotalMatches: src.TotalMatches,
                TeamAWins: src.TeamBWins,
                Draws: src.Draws,
                TeamBWins: src.TeamAWins,
                TeamAGoals: src.TeamBGoals,
                TeamBGoals: src.TeamAGoals,
                RecentMatches: src.RecentMatches);
    }

    /// <summary>
    /// Finner VM-kampene de to lagene har spilt mot hverandre (dvs. har et registrert
    /// resultat), nyeste først. Kobler kampoppsettet mot resultat-tabellen, akkurat som
    /// <see cref="GetWorldCupMatchesAsync"/> gjør for form. Returnerer tom liste hvis
    /// avhengighetene ikke er satt (enhetstester) eller lagene ikke har møttes ennå.
    /// </summary>
    private async Task<IReadOnlyList<HeadToHeadMatch>> GetWorldCupHeadToHeadMatchesAsync(
        string teamA, string teamB, CancellationToken ct)
    {
        if (_scopeFactory is null || _scheduleProvider is null)
        {
            return Array.Empty<HeadToHeadMatch>();
        }

        var meetings = _scheduleProvider.Current.GetAllMatches()
            .Where(m =>
                (string.Equals(m.HomeTeam, teamA, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(m.AwayTeam, teamB, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(m.HomeTeam, teamB, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(m.AwayTeam, teamA, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (meetings.Count == 0)
        {
            return Array.Empty<HeadToHeadMatch>();
        }

        var matchIds = meetings.Select(m => m.Id).ToHashSet();

        Dictionary<int, Models.MatchResult> resultsById;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var results = await db.MatchResults
                .Where(r => matchIds.Contains(r.MatchId))
                .AsNoTracking()
                .ToListAsync(ct);
            resultsById = results.ToDictionary(r => r.MatchId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Kunne ikke hente VM-resultater for H2H-overlay ({A}-{B})", teamA, teamB);
            return Array.Empty<HeadToHeadMatch>();
        }

        var entries = new List<(DateTime Date, HeadToHeadMatch Match)>();
        foreach (var match in meetings)
        {
            if (!resultsById.TryGetValue(match.Id, out var result)) continue;
            if (string.IsNullOrWhiteSpace(match.HomeTeam) || string.IsNullOrWhiteSpace(match.AwayTeam)) continue;

            entries.Add((match.Date, new HeadToHeadMatch(
                Date: match.Date.ToString("yyyy-MM-dd"),
                HomeTeam: match.HomeTeam.ToUpperInvariant(),
                AwayTeam: match.AwayTeam.ToUpperInvariant(),
                HomeScore: result.HomeScore,
                AwayScore: result.AwayScore,
                Competition: "VM 2026",
                Venue: null)));
        }

        return entries
            .OrderByDescending(e => e.Date)
            .Select(e => e.Match)
            .ToList();
    }

    /// <summary>
    /// Slår sammen spilte VM-kamper mellom lagene med eksisterende seed/ekstern-historikk.
    /// VM-kampene legges først i «siste møter» (nyeste først), og samletellere (seire,
    /// uavgjort, mål) oppdateres fra <paramref name="teamA"/>s perspektiv. Uten VM-kamper
    /// returneres base uendret. Hvis base mangler bygges en respons kun fra VM-kampene.
    /// </summary>
    public static HeadToHeadResponse? WithWorldCupHeadToHead(
        HeadToHeadResponse? baseH2h,
        string teamA,
        string teamB,
        IReadOnlyList<HeadToHeadMatch> worldCupMatches)
    {
        if (worldCupMatches.Count == 0) return baseH2h;

        int teamAWins = 0, draws = 0, teamBWins = 0, teamAGoals = 0, teamBGoals = 0;
        foreach (var m in worldCupMatches)
        {
            var teamAIsHome = string.Equals(m.HomeTeam, teamA, StringComparison.OrdinalIgnoreCase);
            var teamAScore = teamAIsHome ? m.HomeScore : m.AwayScore;
            var teamBScore = teamAIsHome ? m.AwayScore : m.HomeScore;

            teamAGoals += teamAScore;
            teamBGoals += teamBScore;
            if (teamAScore > teamBScore) teamAWins++;
            else if (teamAScore < teamBScore) teamBWins++;
            else draws++;
        }

        var baseMatches = baseH2h?.RecentMatches ?? Array.Empty<HeadToHeadMatch>();
        var combined = worldCupMatches.Concat(baseMatches).Take(5).ToList();

        if (baseH2h is null)
        {
            return new HeadToHeadResponse(
                TeamA: teamA,
                TeamB: teamB,
                TotalMatches: worldCupMatches.Count,
                TeamAWins: teamAWins,
                Draws: draws,
                TeamBWins: teamBWins,
                TeamAGoals: teamAGoals,
                TeamBGoals: teamBGoals,
                RecentMatches: combined);
        }

        return baseH2h with
        {
            TotalMatches = baseH2h.TotalMatches + worldCupMatches.Count,
            TeamAWins = baseH2h.TeamAWins + teamAWins,
            Draws = baseH2h.Draws + draws,
            TeamBWins = baseH2h.TeamBWins + teamBWins,
            TeamAGoals = baseH2h.TeamAGoals + teamAGoals,
            TeamBGoals = baseH2h.TeamBGoals + teamBGoals,
            RecentMatches = combined,
        };
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
            LastWorldCupResult: NullIfEmpty(external.LastWorldCupResult) ?? seed.LastWorldCupResult);

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
