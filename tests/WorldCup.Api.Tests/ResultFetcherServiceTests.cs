using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class ResultFetcherServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _jsonPath;
    private readonly MatchScheduleProvider _scheduleProvider;
    private readonly MatchFileWriter _matchFileWriter;

    public ResultFetcherServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _jsonPath = Path.Combine(_tempDir, "matches.json");
        WriteMatches([]);

        _scheduleProvider = new MatchScheduleProvider(_jsonPath);
        var writerOptions = Options.Create(new MatchFileWriterOptions { JsonPath = _jsonPath });
        _matchFileWriter = new MatchFileWriter(
            _scheduleProvider,
            Substitute.For<ILogger<MatchFileWriter>>(),
            writerOptions);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void WriteMatches(IReadOnlyList<MatchEntry> matches)
    {
        var serializerOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(_jsonPath, JsonSerializer.Serialize(matches, serializerOptions));
    }

    private Wc2026ApiClient BuildApiClient(HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wc2026Api:BaseUrl"] = "https://example.test",
                ["Wc2026Api:ApiKey"] = "test-key",
            })
            .Build();

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        return new Wc2026ApiClient(httpClient, config, Substitute.For<ILogger<Wc2026ApiClient>>());
    }

    private TeamCodeMapper BuildTeamCodeMapper(string teamsJsonPath)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(Path.GetDirectoryName(teamsJsonPath)!);
        Environment.SetEnvironmentVariable("TEAMS_JSON_PATH", teamsJsonPath);
        try
        {
            return new TeamCodeMapper(env, Substitute.For<ILogger<TeamCodeMapper>>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEAMS_JSON_PATH", null);
        }
    }

    private static string ResolveTeamsJsonPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "data", "teams.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        var json = """
            {
              "BRA": { "code": "BRA", "name": "Brasil", "flag": "🇧🇷" },
              "GER": { "code": "GER", "name": "Tyskland", "flag": "🇩🇪" }
            }
            """;
        var tmpPath = Path.Combine(Path.GetTempPath(), "teams_rfs_test.json");
        File.WriteAllText(tmpPath, json);
        return tmpPath;
    }

    [Fact]
    public async Task CheckForFixtureUpdates_NoUndeterminedMatches_DoesNotCallApi()
    {
        var allDeterminedMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 1,
                Date = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc),
                Stage = "group-1",
                HomeTeam = "BRA",
                AwayTeam = "GER",
                VenueId = "venue-1",
            }
        };
        WriteMatches(allDeterminedMatches);
        _scheduleProvider.Reload(allDeterminedMatches);

        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });

        var apiClient = BuildApiClient(handler);
        var teamMapper = BuildTeamCodeMapper(ResolveTeamsJsonPath());
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var service = new ResultFetcherService(
            scopeFactory, _scheduleProvider, apiClient, teamMapper, _matchFileWriter,
            Substitute.For<ILogger<ResultFetcherService>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try { await service.StartAsync(cts.Token); } catch { }
        await Task.Delay(300);
        try { await service.StopAsync(CancellationToken.None); } catch { }

        callCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckForFixtureUpdates_ManualOverrideMatches_AreSkipped()
    {
        var manualOverrideMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 50,
                Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
                Stage = "quarter-final",
                HomeTeam = null,
                AwayTeam = null,
                HomePlaceholder = "W Match 48",
                AwayPlaceholder = "W Match 49",
                VenueId = "venue-qf",
                ManualOverride = true,
            }
        };
        WriteMatches(manualOverrideMatches);
        _scheduleProvider.Reload(manualOverrideMatches);

        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });

        var apiClient = BuildApiClient(handler);
        var teamMapper = BuildTeamCodeMapper(ResolveTeamsJsonPath());
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var service = new ResultFetcherService(
            scopeFactory, _scheduleProvider, apiClient, teamMapper, _matchFileWriter,
            Substitute.For<ILogger<ResultFetcherService>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try { await service.StartAsync(cts.Token); } catch { }
        await Task.Delay(300);
        try { await service.StopAsync(CancellationToken.None); } catch { }

        callCount.Should().Be(0,
            "ManualOverride matches must not trigger an API call for fixture updates");
    }

    [Fact]
    public async Task CheckForFixtureUpdates_BothTeamCodesNull_DoesNotWriteFile()
    {
        var undeterminedMatch = new MatchEntry
        {
            Id = 60,
            Date = new DateTime(2026, 7, 5, 18, 0, 0, DateTimeKind.Utc),
            Stage = "semi-final",
            HomeTeam = null,
            AwayTeam = null,
            HomePlaceholder = "W QF1",
            AwayPlaceholder = "W QF2",
            VenueId = "venue-sf",
            ManualOverride = false,
        };
        WriteMatches([undeterminedMatch]);
        _scheduleProvider.Reload([undeterminedMatch]);

        // No team codes, and the names don't map to any local code -> both codes resolve null.
        var apiDto = new List<object>
        {
            new { match_number = 60, home_team = "Unknown FC", away_team = "Mystery United", kickoff_utc = undeterminedMatch.Date }
        };
        var apiJson = JsonSerializer.Serialize(apiDto);

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(apiJson, Encoding.UTF8, "application/json")
            });

        var apiClient = BuildApiClient(handler);

        var teamsJson = """
            {
              "BRA": { "code": "BRA", "name": "Brasil", "flag": "🇧🇷" }
            }
            """;
        var tempTeamsPath = Path.Combine(_tempDir, "teams_null_test.json");
        File.WriteAllText(tempTeamsPath, teamsJson);
        var teamMapper = BuildTeamCodeMapper(tempTeamsPath);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var originalJson = File.ReadAllText(_jsonPath);

        var service = new ResultFetcherService(
            scopeFactory, _scheduleProvider, apiClient, teamMapper, _matchFileWriter,
            Substitute.For<ILogger<ResultFetcherService>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        try { await service.StartAsync(cts.Token); } catch { }
        await Task.Delay(500);
        try { await service.StopAsync(CancellationToken.None); } catch { }

        var afterJson = File.ReadAllText(_jsonPath);
        afterJson.Should().Be(originalJson,
            "when both team codes are null from mapper, the file must not be modified");
    }

    [Fact]
    public async Task CheckForFixtureUpdates_ValidTeamCodes_WritesUpdatedTeamsToFile()
    {
        var undeterminedMatch = new MatchEntry
        {
            Id = 1,
            Date = new DateTime(2026, 7, 5, 18, 0, 0, DateTimeKind.Utc),
            Stage = "semi-final",
            HomeTeam = null,
            AwayTeam = null,
            HomePlaceholder = "W QF1",
            AwayPlaceholder = "W QF2",
            VenueId = "venue-sf",
            ManualOverride = false,
        };

        WriteMatches([undeterminedMatch]);
        _scheduleProvider.Reload([undeterminedMatch]);

        var apiDto = new[]
        {
            new { match_number = 1, home_team = "Brasil", away_team = "Tyskland", home_team_code = "BRA", away_team_code = "GER", kickoff_utc = undeterminedMatch.Date }
        };
        var apiJson = JsonSerializer.Serialize(apiDto);

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(apiJson, Encoding.UTF8, "application/json")
            });

        var apiClient = BuildApiClient(handler);

        var teamsJson = """
            {
              "BRA": { "code": "BRA", "name": "Brasil", "flag": "🇧🇷" },
              "GER": { "code": "GER", "name": "Tyskland", "flag": "🇩🇪" }
            }
            """;
        var tempTeamsPath = Path.Combine(_tempDir, "teams_write_test.json");
        File.WriteAllText(tempTeamsPath, teamsJson);
        var teamMapper = BuildTeamCodeMapper(tempTeamsPath);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var service = new ResultFetcherService(
            scopeFactory, _scheduleProvider, apiClient, teamMapper, _matchFileWriter,
            Substitute.For<ILogger<ResultFetcherService>>());

        var method = typeof(ResultFetcherService).GetMethod(
            "CheckForFixtureUpdatesAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = (Task)method!.Invoke(service, [CancellationToken.None])!;
        await task;

        var written = JsonSerializer.Deserialize<List<MatchEntry>>(
            File.ReadAllText(_jsonPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        written.Should().NotBeNull();
        var updated = written!.FirstOrDefault(m => m.Id == 1);
        updated.Should().NotBeNull();
        updated!.HomeTeam.Should().Be("BRA");
        updated.AwayTeam.Should().Be("GER");
    }

    private static MatchSchedule ScheduleWith(params MatchEntry[] matches) => new(matches);

    [Fact]
    public async Task GetCompletedMatchesAsync_ParsesRealApiShape_IntoScoreAndMatchNumber()
    {
        // Regression: the WC2026 API returns flat snake_case (match_number, home_team,
        // home_score, home_pen, phase) — not camelCase with a nested score.ft. When the DTO
        // didn't match, every field bound to default and results were silently skipped.
        // This is a verbatim sample of the live /matches?status=completed payload.
        const string realPayload = """
            [
              {
                "id": 2,
                "match_number": 1,
                "round": "group",
                "group_name": "A",
                "home_team": "Mexico",
                "home_team_code": "MEX",
                "away_team": "South Africa",
                "away_team_code": "RSA",
                "kickoff_utc": "2026-06-11T19:00:00.000Z",
                "home_score": 2,
                "away_score": 0,
                "home_pen": null,
                "away_pen": null,
                "status": "completed",
                "phase": "FT"
              }
            ]
            """;

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(realPayload, Encoding.UTF8, "application/json")
            });

        var apiClient = BuildApiClient(handler);

        var matches = await apiClient.GetCompletedMatchesAsync();

        matches.Should().HaveCount(1);
        var match = matches[0];
        match.MatchNumber.Should().Be(1);
        match.Home.Should().Be("Mexico");
        match.HomeCode.Should().Be("MEX");
        match.AwayCode.Should().Be("RSA");
        match.KickoffAt.Should().Be(new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc));
        match.HomeScore.Should().Be(2);
        match.AwayScore.Should().Be(0);
        match.HomePen.Should().BeNull();
        match.Phase.Should().Be("FT");
    }

    [Fact]
    public async Task GetCompletedMatchesAsync_KnockoutDecidedOnPenalties_KeepsAfterEtScoreSeparateFromPens()
    {
        // A knockout game that went 1-1 after 90', 2-2 after extra time, won 4-3 on penalties.
        // home_score/away_score carry the after-ET score; penalties are reported separately
        // and must not leak into the score (we score predictions on the after-ET result).
        const string penaltyPayload = """
            [
              {
                "match_number": 90,
                "home_team_code": "BRA",
                "away_team_code": "GER",
                "kickoff_utc": "2026-07-10T19:00:00.000Z",
                "home_score": 2,
                "away_score": 2,
                "home_pen": 4,
                "away_pen": 3,
                "status": "completed",
                "phase": "FT_PEN"
              }
            ]
            """;

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(penaltyPayload, Encoding.UTF8, "application/json")
            });

        var match = (await BuildApiClient(handler).GetCompletedMatchesAsync()).Single();

        match.HomeScore.Should().Be(2);
        match.AwayScore.Should().Be(2);
        match.HomePen.Should().Be(4);
        match.AwayPen.Should().Be(3);
        match.Phase.Should().Be("FT_PEN");
    }

    private Wc2026ApiClient BuildApiClientNoHttp() =>
        BuildApiClient(new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }));

    [Fact]
    public void MapToLocalMatchId_ResolvesByMatchNumber_RegardlessOfKickoffTime()
    {
        var schedule = ScheduleWith(new MatchEntry
        {
            Id = 42,
            Date = new DateTime(2026, 6, 20, 16, 0, 0, DateTimeKind.Utc),
            Stage = "group-2",
            HomeTeam = "BRA",
            AwayTeam = "GER",
            VenueId = "venue-1",
        });

        var apiClient = BuildApiClientNoHttp();

        // Kickoff time is hours off, but the match number is authoritative.
        var matchId = apiClient.MapToLocalMatchId(
            matchNumber: 42,
            kickoffAt: new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            schedule);

        matchId.Should().Be(42);
    }

    [Fact]
    public void MapToLocalMatchId_TimeFallback_NormalizesOffsetKickoffToUtc()
    {
        // Regression: matches.json stores kickoff in UTC ("...Z"). When the upstream payload
        // delivers kickoff with a numeric offset, System.Text.Json yields a Local/converted
        // DateTime. A raw DateTime subtraction ignores Kind and would land hours away from the
        // UTC fixture, so the result never mapped and we logged "No result available".
        var schedule = ScheduleWith(new MatchEntry
        {
            Id = 7,
            Date = new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc),
            Stage = "group-1",
            HomeTeam = "MEX",
            AwayTeam = "RSA",
            VenueId = "azteca",
        });

        // Same instant as 19:00Z, expressed with a -07:00 offset — exactly what the deserializer
        // hands us for a Pacific-time venue. Match number is unknown so the time fallback runs.
        var offsetKickoff = JsonSerializer.Deserialize<DateTime>("\"2026-06-11T12:00:00-07:00\"");

        var apiClient = BuildApiClientNoHttp();

        var matchId = apiClient.MapToLocalMatchId(matchNumber: 9999, offsetKickoff, schedule);

        matchId.Should().Be(7);
    }

    [Fact]
    public void MapToLocalMatchId_TimeFallback_DoesNotGuessAmongSimultaneousKickoffs()
    {
        // Final-round group games kick off at the same instant. The time fallback must refuse
        // to guess, otherwise both DTOs collapse onto one fixture.
        var kickoff = new DateTime(2026, 6, 26, 20, 0, 0, DateTimeKind.Utc);
        var schedule = ScheduleWith(
            new MatchEntry { Id = 71, Date = kickoff, Stage = "group-3", HomeTeam = "ARG", AwayTeam = "FRA", VenueId = "v1" },
            new MatchEntry { Id = 72, Date = kickoff, Stage = "group-3", HomeTeam = "ESP", AwayTeam = "ITA", VenueId = "v2" });

        var apiClient = BuildApiClientNoHttp();

        // Unknown match number + ambiguous time => no match (rather than the wrong one).
        apiClient.MapToLocalMatchId(matchNumber: 9999, kickoff, schedule).Should().BeNull();

        // But a known match number still resolves the correct one of the pair.
        apiClient.MapToLocalMatchId(matchNumber: 72, kickoff, schedule).Should().Be(72);
    }

    private static IReadOnlyList<MatchEntry> InvokeResolvable(MatchSchedule schedule, IReadOnlySet<int> completed)
    {
        var method = typeof(ResultFetcherService).GetMethod(
            "GetResolvableUndeterminedKnockout",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (List<MatchEntry>)method!.Invoke(null, [schedule, completed])!;
    }

    [Fact]
    public void GetResolvableUndeterminedKnockout_RoundOf32_ResolvesOnlyWhenEveryGroupMatchComplete()
    {
        // Round-of-32 placeholders name group positions ("2. plass gruppe A"). The bracket — and
        // the best-third allocation — is only fixed once *every* group match has been played.
        var groupMatches = Enumerable.Range(1, 3).Select(i => new MatchEntry
        {
            Id = i,
            Date = new DateTime(2026, 6, 27, 23, 30, 0, DateTimeKind.Utc),
            Stage = "group-3",
            HomeTeam = "BRA",
            AwayTeam = "GER",
            VenueId = "venue",
        }).ToList();

        var roundOf32 = new MatchEntry
        {
            Id = 73,
            Date = new DateTime(2026, 6, 28, 19, 0, 0, DateTimeKind.Utc),
            Stage = "round-of-32",
            HomePlaceholder = "2. plass gruppe A",
            AwayPlaceholder = "2. plass gruppe B",
            VenueId = "venue",
        };

        var schedule = new MatchSchedule(groupMatches.Append(roundOf32).ToList());

        InvokeResolvable(schedule, new HashSet<int> { 1, 2 }).Should().BeEmpty(
            "the round-of-32 bracket is not settled until every group match is played");

        InvokeResolvable(schedule, new HashSet<int> { 1, 2, 3 })
            .Select(m => m.Id).Should().ContainSingle().Which.Should().Be(73);
    }

    [Fact]
    public void GetResolvableUndeterminedKnockout_LaterRound_ResolvesOnReferencedMatches()
    {
        // Later rounds name specific feeder games ("Vinner kamp 74").
        var roundOf16 = new MatchEntry
        {
            Id = 89,
            Date = new DateTime(2026, 7, 1, 19, 0, 0, DateTimeKind.Utc),
            Stage = "round-of-16",
            HomePlaceholder = "Vinner kamp 74",
            AwayPlaceholder = "Vinner kamp 77",
            VenueId = "venue",
        };
        var schedule = new MatchSchedule([roundOf16]);

        InvokeResolvable(schedule, new HashSet<int> { 74 }).Should().BeEmpty();
        InvokeResolvable(schedule, new HashSet<int> { 74, 77 })
            .Select(m => m.Id).Should().ContainSingle().Which.Should().Be(89);
    }

    [Fact]
    public void GetResolvableUndeterminedKnockout_SkipsManualOverrideAndDeterminedFixtures()
    {
        var determined = new MatchEntry
        {
            Id = 90,
            Stage = "round-of-16",
            HomeTeam = "BRA",
            AwayTeam = "GER",
            HomePlaceholder = "Vinner kamp 73",
            AwayPlaceholder = "Vinner kamp 75",
            VenueId = "venue",
        };
        var overridden = new MatchEntry
        {
            Id = 91,
            Stage = "round-of-16",
            HomePlaceholder = "Vinner kamp 76",
            AwayPlaceholder = "Vinner kamp 78",
            ManualOverride = true,
            VenueId = "venue",
        };
        var schedule = new MatchSchedule([determined, overridden]);

        InvokeResolvable(schedule, new HashSet<int> { 73, 75, 76, 78 }).Should().BeEmpty(
            "determined fixtures need no resolution and manual overrides must never be touched");
    }
}

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(handler(request));
    }
}
