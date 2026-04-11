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
                Stage = "group",
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

        var apiDto = new List<object>
        {
            new { matchNumber = 60, home = "Unknown FC", away = "Mystery United", kickoffAt = undeterminedMatch.Date }
        };
        var apiJson = JsonSerializer.Serialize(apiDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

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
}

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(handler(request));
    }
}
