using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class MatchFileWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _jsonPath;
    private MatchScheduleProvider _scheduleProvider = null!;
    private ILogger<MatchFileWriter> _logger = null!;

    public MatchFileWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _jsonPath = Path.Combine(_tempDir, "matches.json");

        var initialMatches = new List<MatchEntry>
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
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(_jsonPath, JsonSerializer.Serialize(initialMatches, options));

        _scheduleProvider = new MatchScheduleProvider(_jsonPath);
        _logger = Substitute.For<ILogger<MatchFileWriter>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private MatchFileWriter CreateWriter()
    {
        var opts = Options.Create(new MatchFileWriterOptions { JsonPath = _jsonPath });
        return new MatchFileWriter(_scheduleProvider, _logger, opts);
    }

    [Fact]
    public async Task WriteAsync_WritesMatchesToJsonFile()
    {
        var writer = CreateWriter();
        var newMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 10,
                Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
                Stage = "final",
                HomeTeam = "ARG",
                AwayTeam = "POR",
                VenueId = "venue-final",
            }
        };

        await writer.WriteAsync(newMatches);

        var json = File.ReadAllText(_jsonPath);
        json.Should().Contain("ARG");
        json.Should().Contain("POR");
    }

    [Fact]
    public async Task WriteAsync_NoTmpFileLeftBehind_AfterWrite()
    {
        var writer = CreateWriter();
        var newMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 1,
                Date = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc),
                Stage = "group",
                HomeTeam = "ESP",
                AwayTeam = "FRA",
                VenueId = "venue-1",
            }
        };

        await writer.WriteAsync(newMatches);

        File.Exists($"{_jsonPath}.tmp").Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_ReloadsScheduleProvider_WithNewMatches()
    {
        var writer = CreateWriter();
        var newMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 42,
                Date = new DateTime(2026, 6, 20, 18, 0, 0, DateTimeKind.Utc),
                Stage = "round of 16",
                HomeTeam = "NED",
                AwayTeam = "ARG",
                VenueId = "venue-r16",
            }
        };

        await writer.WriteAsync(newMatches);

        _scheduleProvider.Current.GetMatch(42).Should().NotBeNull();
        _scheduleProvider.Current.GetMatch(42)!.HomeTeam.Should().Be("NED");
    }

    [Fact]
    public async Task WriteAsync_PreviousMatchGone_AfterReload()
    {
        var writer = CreateWriter();

        _scheduleProvider.Current.GetMatch(1).Should().NotBeNull();

        var replacementMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 99,
                Date = new DateTime(2026, 7, 18, 18, 0, 0, DateTimeKind.Utc),
                Stage = "final",
                HomeTeam = "BRA",
                AwayTeam = "ESP",
                VenueId = "venue-final",
            }
        };

        await writer.WriteAsync(replacementMatches);

        _scheduleProvider.Current.GetMatch(1).Should().BeNull();
        _scheduleProvider.Current.GetMatch(99).Should().NotBeNull();
    }
}
