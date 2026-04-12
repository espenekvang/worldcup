using System.IO;
using System.Text.Json;
using FluentAssertions;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class MatchScheduleProviderTests : IDisposable
{
    private readonly string _tempJsonPath;

    public MatchScheduleProviderTests()
    {
        _tempJsonPath = Path.GetTempFileName();
        WriteMatchesToFile(_tempJsonPath, BuildSampleMatches());
    }

    public void Dispose()
    {
        if (File.Exists(_tempJsonPath))
        {
            File.Delete(_tempJsonPath);
        }
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

    private static void WriteMatchesToFile(string path, IReadOnlyList<MatchEntry> matches)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(path, JsonSerializer.Serialize(matches, options));
    }

    [Fact]
    public void Current_AfterConstruction_ReturnsLoadedSchedule()
    {
        var provider = new MatchScheduleProvider(_tempJsonPath);

        var all = provider.Current.GetAllMatches();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void Current_AfterConstruction_ContainsExpectedMatch()
    {
        var provider = new MatchScheduleProvider(_tempJsonPath);

        var match = provider.Current.GetMatch(1);

        match.Should().NotBeNull();
        match!.HomeTeam.Should().Be("BRA");
        match.AwayTeam.Should().Be("GER");
    }

    [Fact]
    public void Reload_WithNewMatchList_ReplacesCurrentSchedule()
    {
        var provider = new MatchScheduleProvider(_tempJsonPath);

        var newMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 99,
                Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
                Stage = "final",
                HomeTeam = "ARG",
                AwayTeam = "POR",
                VenueId = "venue-final",
            }
        };
        provider.Reload(newMatches);

        var all = provider.Current.GetAllMatches();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(99);
        all[0].HomeTeam.Should().Be("ARG");
    }

    [Fact]
    public void Reload_WithNewMatchList_OldMatchesNoLongerPresent()
    {
        var provider = new MatchScheduleProvider(_tempJsonPath);

        var newMatches = new List<MatchEntry>
        {
            new MatchEntry
            {
                Id = 99,
                Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
                Stage = "final",
                HomeTeam = "ARG",
                AwayTeam = "POR",
                VenueId = "venue-final",
            }
        };
        provider.Reload(newMatches);

        provider.Current.GetMatch(1).Should().BeNull();
        provider.Current.GetMatch(2).Should().BeNull();
    }

    [Fact]
    public void Reload_WithJsonPath_ReplacesCurrentSchedule()
    {
        var provider = new MatchScheduleProvider(_tempJsonPath);
        var newTempPath = Path.GetTempFileName();
        try
        {
            var updatedMatches = new List<MatchEntry>
            {
                new MatchEntry
                {
                    Id = 50,
                    Date = new DateTime(2026, 6, 20, 18, 0, 0, DateTimeKind.Utc),
                    Stage = "round of 16",
                    HomeTeam = "NED",
                    AwayTeam = "ARG",
                    VenueId = "venue-3",
                }
            };
            WriteMatchesToFile(newTempPath, updatedMatches);

            provider.Reload(newTempPath);

            provider.Current.GetAllMatches().Should().HaveCount(1);
            provider.Current.GetMatch(50)!.HomeTeam.Should().Be("NED");
        }
        finally
        {
            File.Delete(newTempPath);
        }
    }

    [Fact]
    public async Task Reload_ConcurrentCalls_DoesNotThrowAndCurrentRemainsValid()
    {
        var provider = new MatchScheduleProvider(_tempJsonPath);
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            try
            {
                var matches = new List<MatchEntry>
                {
                    new MatchEntry
                    {
                        Id = i,
                        Date = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc),
                        Stage = "group",
                        HomeTeam = "BRA",
                        AwayTeam = "GER",
                        VenueId = "venue-1",
                    }
                };

                provider.Reload(matches);
                provider.Current.GetAllMatches();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        await Task.WhenAll(tasks);

        exceptions.Should().BeEmpty();
        provider.Current.GetAllMatches().Should().HaveCount(1);
        provider.Current.GetAllMatches()[0].Should().NotBeNull();
    }
}
