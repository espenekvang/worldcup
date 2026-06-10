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
    public void WithWorldCupForm_uten_vm_kamper_returnerer_base_uendret()
    {
        var baseStats = SeedStats(recentForm: "WWDWL");

        var result = TeamStatsService.WithWorldCupForm(baseStats, "BRA", Array.Empty<RecentMatchEntry>());

        result.Should().BeSameAs(baseStats);
    }

    [Fact]
    public void WithWorldCupForm_legger_vm_kamper_forst_og_regner_form_pa_nytt()
    {
        var baseStats = SeedStats(recentForm: "WWDWL");

        // VM-kamper kommer inn nyeste først (slik GetWorldCupMatchesAsync leverer).
        var wc = new[]
        {
            new RecentMatchEntry("2026-06-20", "FRA", "away", 1, 2, "L", "VM 2026"),
            new RecentMatchEntry("2026-06-15", "MEX", "home", 3, 0, "W", "VM 2026"),
            new RecentMatchEntry("2026-06-11", "RSA", "home", 2, 2, "D", "VM 2026"),
        };

        var result = TeamStatsService.WithWorldCupForm(baseStats, "BRA", wc)!;

        // Nyeste VM-kamp ligger først i lista.
        result.RecentMatches.First().Opponent.Should().Be("FRA");
        result.RecentMatches.Should().HaveCount(3);
        // recentForm er eldste først: D (RSA), W (MEX), L (FRA).
        result.RecentForm.Should().Be("DWL");
        // Snitt regnes fra de spilte kampene: (1+3+2)/3 og (2+0+2)/3.
        result.GoalsScoredAvg.Should().Be(2.0);
        result.GoalsConcededAvg.Should().BeApproximately(1.33, 0.001);
        // Stabile seed-felt beholdes.
        result.FifaRank.Should().Be(5);
        result.Manager.Should().Be("Test Manager");
    }

    [Fact]
    public void WithWorldCupForm_form_bruker_kun_siste_fem_kamper()
    {
        var baseStats = SeedStats(recentForm: null);
        var wc = new[]
        {
            new RecentMatchEntry("2026-07-06", "ARG", "home", 1, 0, "W", "VM 2026"),
            new RecentMatchEntry("2026-07-01", "ESP", "home", 1, 0, "W", "VM 2026"),
            new RecentMatchEntry("2026-06-25", "GER", "home", 1, 0, "W", "VM 2026"),
            new RecentMatchEntry("2026-06-20", "FRA", "away", 0, 1, "L", "VM 2026"),
            new RecentMatchEntry("2026-06-15", "MEX", "home", 2, 2, "D", "VM 2026"),
            new RecentMatchEntry("2026-06-11", "RSA", "home", 3, 0, "W", "VM 2026"),
        };

        var result = TeamStatsService.WithWorldCupForm(baseStats, "BRA", wc)!;

        // Kun de fem nyeste teller, eldste først: MEX(D), FRA(L), GER(W), ESP(W), ARG(W).
        result.RecentForm.Should().Be("DLWWW");
    }

    [Fact]
    public void WithWorldCupForm_bygger_minimal_respons_naar_base_mangler()
    {
        var wc = new[]
        {
            new RecentMatchEntry("2026-06-15", "MEX", "home", 3, 0, "W", "VM 2026"),
        };

        var result = TeamStatsService.WithWorldCupForm(null, "BRA", wc)!;

        result.Should().NotBeNull();
        result.TeamCode.Should().Be("BRA");
        result.RecentForm.Should().Be("W");
        result.RecentMatches.Should().HaveCount(1);
        result.GoalsScoredAvg.Should().Be(3.0);
    }

    private static TeamStatsResponse SeedStats(string? recentForm) =>
        new(
            TeamCode: "BRA",
            FifaRank: 5,
            Manager: "Test Manager",
            StarPlayer: "Star",
            PreferredFormation: "4-3-3",
            GoalsScoredAvg: 1.8,
            GoalsConcededAvg: 0.9,
            RecentForm: recentForm,
            RecentMatches: Array.Empty<RecentMatchEntry>(),
            KeyAbsences: Array.Empty<string>(),
            LastWorldCupResult: "Kvartfinale 2022");

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
