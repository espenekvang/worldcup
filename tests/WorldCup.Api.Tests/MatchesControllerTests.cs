using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorldCup.Api.Controllers;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class MatchesControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _jsonPath;
    private readonly MatchScheduleProvider _scheduleProvider;
    private readonly MatchFileWriter _matchFileWriter;
    private readonly TeamCodeMapper _teamCodeMapper;

    public MatchesControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _jsonPath = Path.Combine(_tempDir, "matches.json");

        var serializerOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(_jsonPath, JsonSerializer.Serialize(BuildSampleMatches(), serializerOptions));

        _scheduleProvider = new MatchScheduleProvider(_jsonPath);

        var writerOptions = Options.Create(new MatchFileWriterOptions { JsonPath = _jsonPath });
        _matchFileWriter = new MatchFileWriter(
            _scheduleProvider,
            Substitute.For<ILogger<MatchFileWriter>>(),
            writerOptions);

        var teamsJsonPath = ResolveTeamsJsonPath();
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(Path.GetDirectoryName(teamsJsonPath)!);

        Environment.SetEnvironmentVariable("TEAMS_JSON_PATH", teamsJsonPath);
        try
        {
            _teamCodeMapper = new TeamCodeMapper(env, Substitute.For<ILogger<TeamCodeMapper>>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEAMS_JSON_PATH", null);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
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
              "GER": { "code": "GER", "name": "Tyskland", "flag": "🇩🇪" },
              "ESP": { "code": "ESP", "name": "Spania", "flag": "🇪🇸" }
            }
            """;
        var tmpPath = Path.Combine(Path.GetTempPath(), "teams_test.json");
        File.WriteAllText(tmpPath, json);
        return tmpPath;
    }

    private static List<MatchEntry> BuildSampleMatches() =>
    [
        new MatchEntry
        {
            Id = 1,
            Date = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc),
            Stage = "group",
            HomeTeam = "BRA",
            AwayTeam = "GER",
            VenueId = "venue-1",
        },
        new MatchEntry
        {
            Id = 2,
            Date = new DateTime(2026, 6, 12, 18, 0, 0, DateTimeKind.Utc),
            Stage = "group",
            HomeTeam = "ESP",
            AwayTeam = "FRA",
            VenueId = "venue-2",
        },
    ];

    private MatchesController CreateController() =>
        new MatchesController(_scheduleProvider, _teamCodeMapper, _matchFileWriter);

    [Fact]
    public void GetMatches_ReturnsOkResult()
    {
        var controller = CreateController();

        var result = controller.GetMatches();

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetMatches_ReturnsAllMatches()
    {
        var controller = CreateController();

        var result = controller.GetMatches();

        var ok = result.Result as OkObjectResult;
        var matches = ok!.Value as IEnumerable<MatchResponse>;
        matches.Should().HaveCount(2);
    }

    [Fact]
    public void GetMatches_ReturnsCorrectMatchData()
    {
        var controller = CreateController();

        var result = controller.GetMatches();

        var ok = result.Result as OkObjectResult;
        var matches = (ok!.Value as IEnumerable<MatchResponse>)!.ToList();

        matches.Should().Contain(m => m.Id == 1 && m.HomeTeam == "BRA" && m.AwayTeam == "GER");
        matches.Should().Contain(m => m.Id == 2 && m.HomeTeam == "ESP");
    }

    [Fact]
    public async Task OverrideMatch_NotFound_WhenMatchDoesNotExist()
    {
        var controller = CreateController();
        var request = new AdminMatchOverrideRequest("ARG", null);

        var result = await controller.OverrideMatch(999, request, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OverrideMatch_BadRequest_WhenStageIsGroup()
    {
        var controller = CreateController();
        var request = new AdminMatchOverrideRequest("BRA", null);

        var result = await controller.OverrideMatch(1, request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
