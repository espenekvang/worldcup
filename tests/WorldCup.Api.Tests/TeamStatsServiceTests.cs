using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorldCup.Api.DTOs;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class TeamStatsServiceTests : IDisposable
{
    private readonly string _tempContentRoot;

    public TeamStatsServiceTests()
    {
        _tempContentRoot = Path.Combine(Path.GetTempPath(), "team-stats-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempContentRoot, "data"));

        var seed = new
        {
            teams = new Dictionary<string, object?>
            {
                ["BRA"] = new
                {
                    teamCode = "BRA",
                    fifaRank = 5,
                    manager = "Test Manager",
                    starPlayer = "Star",
                    preferredFormation = "4-3-3",
                    goalsScoredAvg = 1.8,
                    goalsConcededAvg = 0.9,
                    recentForm = "WWDWL",
                    recentMatches = Array.Empty<object>(),
                    keyAbsences = Array.Empty<string>(),
                    lastWorldCupResult = "Kvartfinale 2022",
                },
            },
            headToHead = new Dictionary<string, object?>
            {
                // Canonical (alfabetisk) rekkefølge ARG-BRA. ARG vinner 41, BRA vinner 43.
                ["ARG-BRA"] = new
                {
                    teamA = "ARG",
                    teamB = "BRA",
                    totalMatches = 110,
                    teamAWins = 41,
                    draws = 26,
                    teamBWins = 43,
                    teamAGoals = 163,
                    teamBGoals = 160,
                    recentMatches = Array.Empty<object>(),
                },
            },
        };
        File.WriteAllText(Path.Combine(_tempContentRoot, "data", "teamStats.json"),
            JsonSerializer.Serialize(seed));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempContentRoot))
        {
            Directory.Delete(_tempContentRoot, recursive: true);
        }
    }

    private TeamStatsService CreateService()
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(_tempContentRoot);
        return new TeamStatsService(
            new NoopExternalTeamStatsClient(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<TeamStatsService>.Instance,
            env);
    }

    [Fact]
    public async Task GetTeamStatsAsync_returnerer_seed_for_kjent_lag()
    {
        var svc = CreateService();

        var stats = await svc.GetTeamStatsAsync("BRA", CancellationToken.None);

        stats.Should().NotBeNull();
        stats!.FifaRank.Should().Be(5);
        stats.Manager.Should().Be("Test Manager");
        stats.RecentForm.Should().Be("WWDWL");
    }

    [Fact]
    public async Task GetTeamStatsAsync_er_case_insensitiv_paa_lagkode()
    {
        var svc = CreateService();

        var stats = await svc.GetTeamStatsAsync("bra", CancellationToken.None);

        stats.Should().NotBeNull();
        stats!.TeamCode.Should().Be("BRA");
    }

    [Fact]
    public async Task GetTeamStatsAsync_returnerer_null_for_ukjent_lag()
    {
        var svc = CreateService();

        var stats = await svc.GetTeamStatsAsync("ZZZ", CancellationToken.None);

        stats.Should().BeNull();
    }

    [Fact]
    public async Task GetHeadToHead_returnerer_data_i_canonical_rekkefolge()
    {
        var svc = CreateService();

        // Spør i samme rekkefølge som seed: ARG = A, BRA = B.
        var h2h = await svc.GetHeadToHeadAsync("ARG", "BRA", CancellationToken.None);

        h2h.Should().NotBeNull();
        h2h!.TeamA.Should().Be("ARG");
        h2h.TeamAWins.Should().Be(41);
        h2h.TeamBWins.Should().Be(43);
        h2h.TeamAGoals.Should().Be(163);
        h2h.TeamBGoals.Should().Be(160);
    }

    [Fact]
    public async Task GetHeadToHead_speilvender_tellerne_naar_klient_spor_i_motsatt_rekkefolge()
    {
        var svc = CreateService();

        // Bytt rekkefølge: nå er BRA "team A" sett fra klienten.
        var h2h = await svc.GetHeadToHeadAsync("BRA", "ARG", CancellationToken.None);

        h2h.Should().NotBeNull();
        h2h!.TeamA.Should().Be("BRA");
        h2h.TeamB.Should().Be("ARG");
        // Seier-tellerne må være speilvendt: BRA hadde 43 seire, ARG hadde 41.
        h2h.TeamAWins.Should().Be(43);
        h2h.TeamBWins.Should().Be(41);
        h2h.TeamAGoals.Should().Be(160);
        h2h.TeamBGoals.Should().Be(163);
        h2h.Draws.Should().Be(26); // uavgjort er symmetrisk
        h2h.TotalMatches.Should().Be(110);
    }

    [Fact]
    public async Task GetHeadToHead_returnerer_null_naar_paret_mangler()
    {
        var svc = CreateService();

        var h2h = await svc.GetHeadToHeadAsync("BRA", "NOR", CancellationToken.None);

        h2h.Should().BeNull();
    }

    [Fact]
    public async Task GetHeadToHead_returnerer_null_naar_samme_lag()
    {
        var svc = CreateService();

        var h2h = await svc.GetHeadToHeadAsync("BRA", "BRA", CancellationToken.None);

        h2h.Should().BeNull();
    }

    [Fact]
    public async Task Ekstern_klient_prioriteres_over_seed_naar_den_returnerer_data()
    {
        var external = Substitute.For<IExternalTeamStatsClient>();
        external.GetTeamStatsAsync("BRA", Arg.Any<CancellationToken>())
            .Returns(new TeamStatsResponse(
                TeamCode: "BRA",
                FifaRank: 999,
                Manager: "External Manager",
                StarPlayer: null,
                PreferredFormation: null,
                GoalsScoredAvg: null,
                GoalsConcededAvg: null,
                RecentForm: null,
                RecentMatches: Array.Empty<RecentMatchEntry>(),
                KeyAbsences: Array.Empty<string>(),
                LastWorldCupResult: null));

        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(_tempContentRoot);
        var svc = new TeamStatsService(external, new MemoryCache(new MemoryCacheOptions()), NullLogger<TeamStatsService>.Instance, env);

        var stats = await svc.GetTeamStatsAsync("BRA", CancellationToken.None);

        stats.Should().NotBeNull();
        stats!.FifaRank.Should().Be(999);
        stats.Manager.Should().Be("External Manager");
    }

    [Fact]
    public async Task Merge_faller_tilbake_til_seed_for_felt_ekstern_klient_lar_vaere_null()
    {
        // Ekstern API gir typisk bare form/recent/snitt — resten skal merges fra seed.
        var external = Substitute.For<IExternalTeamStatsClient>();
        external.GetTeamStatsAsync("BRA", Arg.Any<CancellationToken>())
            .Returns(new TeamStatsResponse(
                TeamCode: "BRA",
                FifaRank: null,
                Manager: null,
                StarPlayer: null,
                PreferredFormation: null,
                GoalsScoredAvg: 2.5,
                GoalsConcededAvg: 0.5,
                RecentForm: "WWWWW",
                RecentMatches: new[]
                {
                    new RecentMatchEntry("2026-03-01", "FRA", "home", 3, 0, "W", "Vennskap"),
                },
                KeyAbsences: Array.Empty<string>(),
                LastWorldCupResult: null));

        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(_tempContentRoot);
        var svc = new TeamStatsService(external, new MemoryCache(new MemoryCacheOptions()), NullLogger<TeamStatsService>.Instance, env);

        var stats = await svc.GetTeamStatsAsync("BRA", CancellationToken.None);

        stats.Should().NotBeNull();
        // Eksterne verdier vinner der de er satt:
        stats!.RecentForm.Should().Be("WWWWW");
        stats.GoalsScoredAvg.Should().Be(2.5);
        stats.RecentMatches.Should().HaveCount(1);
        // Seed fyller inn der ekstern er null:
        stats.FifaRank.Should().Be(5);
        stats.Manager.Should().Be("Test Manager");
        stats.StarPlayer.Should().Be("Star");
        stats.PreferredFormation.Should().Be("4-3-3");
        stats.LastWorldCupResult.Should().Be("Kvartfinale 2022");
    }
}
