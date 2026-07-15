using FluentAssertions;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class KnockoutBracketResolverTests
{
    private readonly KnockoutBracketResolver _resolver = new();

    private static MatchEntry Ko(
        int id,
        string stage,
        string? home = null,
        string? away = null,
        string? homePh = null,
        string? awayPh = null,
        bool manualOverride = false) => new()
        {
            Id = id,
            Date = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc).AddDays(id),
            Stage = stage,
            HomeTeam = home,
            AwayTeam = away,
            HomePlaceholder = homePh,
            AwayPlaceholder = awayPh,
            VenueId = "venue",
            ManualOverride = manualOverride,
        };

    private static MatchEntry Find(IReadOnlyList<MatchEntry> matches, int id) => matches.Single(m => m.Id == id);

    [Fact]
    public void Resolve_FillsSemiFinalFromDecisiveQuarterFinalResults()
    {
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", home: "NED", away: "ARG"),
            Ko(98, "quarter-final", home: "BRA", away: "FRA"),
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 98"),
        };
        var scores = new Dictionary<int, MatchScore>
        {
            [97] = new(2, 1), // NED vinner
            [98] = new(0, 1), // FRA vinner
        };

        var resolved = _resolver.Resolve(matches, scores);

        var semi = Find(resolved, 101);
        semi.HomeTeam.Should().Be("NED");
        semi.AwayTeam.Should().Be("FRA");
    }

    [Fact]
    public void Resolve_CascadesThroughMultipleRoundsInOneCall()
    {
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", home: "A", away: "B"),
            Ko(98, "quarter-final", home: "C", away: "D"),
            Ko(99, "quarter-final", home: "E", away: "F"),
            Ko(100, "quarter-final", home: "G", away: "H"),
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 98"),
            Ko(102, "semi-final", homePh: "Vinner kamp 99", awayPh: "Vinner kamp 100"),
            Ko(103, "third-place", homePh: "Taper kamp 101", awayPh: "Taper kamp 102"),
            Ko(104, "final", homePh: "Vinner kamp 101", awayPh: "Vinner kamp 102"),
        };
        var scores = new Dictionary<int, MatchScore>
        {
            [97] = new(1, 0), // A
            [98] = new(1, 0), // C
            [99] = new(1, 0), // E
            [100] = new(1, 0), // G
            [101] = new(2, 1), // A vinner semi -> A til finale, C til bronse
            [102] = new(0, 1), // G vinner semi -> G til finale, E til bronse
        };

        var resolved = _resolver.Resolve(matches, scores);

        var final = Find(resolved, 104);
        final.HomeTeam.Should().Be("A");
        final.AwayTeam.Should().Be("G");

        var bronze = Find(resolved, 103);
        bronze.HomeTeam.Should().Be("C"); // taper kamp 101
        bronze.AwayTeam.Should().Be("E"); // taper kamp 102
    }

    [Fact]
    public void Resolve_LeavesSlotUnresolvedWhenFeederEndedLevel()
    {
        // Uavgjort i sluttspill avgjøres på straffer; vinneren kan ikke utledes fra målene alene.
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", home: "NED", away: "ARG"),
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 98"),
        };
        var scores = new Dictionary<int, MatchScore> { [97] = new(1, 1) };

        var resolved = _resolver.Resolve(matches, scores);

        Find(resolved, 101).HomeTeam.Should().BeNull();
    }

    [Fact]
    public void Resolve_LeavesSlotUnresolvedWhenFeederTeamsUnknown()
    {
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", homePh: "Vinner kamp 89", awayPh: "Vinner kamp 90"), // lag ikke oppløst
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 98"),
        };
        var scores = new Dictionary<int, MatchScore> { [97] = new(2, 1) };

        var resolved = _resolver.Resolve(matches, scores);

        Find(resolved, 101).HomeTeam.Should().BeNull();
    }

    [Fact]
    public void Resolve_LeavesSlotUnresolvedWhenFeederHasNoResult()
    {
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", home: "NED", away: "ARG"),
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 98"),
        };
        var scores = new Dictionary<int, MatchScore>(); // ingen resultater

        var resolved = _resolver.Resolve(matches, scores);

        Find(resolved, 101).HomeTeam.Should().BeNull();
    }

    [Fact]
    public void Resolve_RespectsManualOverride()
    {
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", home: "NED", away: "ARG"),
            Ko(98, "quarter-final", home: "BRA", away: "FRA"),
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 98", manualOverride: true),
        };
        var scores = new Dictionary<int, MatchScore> { [97] = new(2, 1), [98] = new(0, 1) };

        var resolved = _resolver.Resolve(matches, scores);

        Find(resolved, 101).HomeTeam.Should().BeNull(); // ikke overstyrt av auto-oppløsning
    }

    [Fact]
    public void Resolve_LeavesGroupBasedRoundOf32PlaceholdersUntouched()
    {
        // Runde-32-inngangen er gruppestilling-basert («2. plass gruppe E»), ikke kampnummer-basert.
        var matches = new List<MatchEntry>
        {
            Ko(80, "round-of-32", homePh: "2. plass gruppe E", awayPh: "2. plass gruppe I"),
        };

        var resolved = _resolver.Resolve(matches, new Dictionary<int, MatchScore>());

        var m = Find(resolved, 80);
        m.HomeTeam.Should().BeNull();
        m.AwayTeam.Should().BeNull();
    }

    [Fact]
    public void Resolve_DoesNotFillPartiallyKnownFixtureWithDuplicateTeam()
    {
        // Beggeslag ville blitt samme lag → skal avvises (samme vern som oppstrøms-fyllingen).
        var matches = new List<MatchEntry>
        {
            Ko(97, "quarter-final", home: "NED", away: "ARG"),
            // Begge semifinale-slottene peker (feilaktig) på samme kamp/vinner.
            Ko(101, "semi-final", homePh: "Vinner kamp 97", awayPh: "Vinner kamp 97"),
        };
        var scores = new Dictionary<int, MatchScore> { [97] = new(2, 1) };

        var resolved = _resolver.Resolve(matches, scores);

        var semi = Find(resolved, 101);
        semi.HomeTeam.Should().BeNull();
        semi.AwayTeam.Should().BeNull();
    }
}
