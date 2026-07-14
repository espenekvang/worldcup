using FluentAssertions;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class LeagueStatsCalculatorTests
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Carol = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly LeagueStatsCalculator _calculator = new(new ScoringService());

    private static readonly IReadOnlyList<StatMatchInfo> MatchInfos = new List<StatMatchInfo>
    {
        new(1, "group-1", new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc), true),
        new(2, "group-1", new DateTime(2026, 6, 11, 21, 0, 0, DateTimeKind.Utc), true),
        new(3, "round-of-32", new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc), true),
    };

    /// <summary>
    /// Bygger et deterministisk scenario med tre spillere over tre kamper.
    /// Fasit er regnet ut for hånd i testene under.
    /// </summary>
    private (List<StatMember> members, List<StatPrediction> predictions, List<StatResult> results) BuildScenario()
    {
        var members = new List<StatMember>
        {
            new(Alice, "Alice", null),
            new(Bob, "Bob", null),
            new(Carol, "Carol", null),
        };

        var t0 = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc);
        var results = new List<StatResult>
        {
            new(1, 2, 1, t0),               // hjemmeseier
            new(2, 0, 0, t0.AddHours(3)),   // uavgjort
            new(3, 1, 2, t0.AddHours(6)),   // borteseier
        };

        var predictions = new List<StatPrediction>
        {
            // Alice: treffer utfall i alle tre, ett blinkskudd (kamp 1)
            new(Alice, 1, 2, 1),
            new(Alice, 2, 1, 1),
            new(Alice, 3, 0, 1),
            // Bob: to utfallstreff, bommer stygt på kamp 3
            new(Bob, 1, 1, 0),
            new(Bob, 2, 2, 2),
            new(Bob, 3, 3, 0),
            // Carol: to blinkskudd, bommer stygt på kamp 1
            new(Carol, 1, 0, 3),
            new(Carol, 2, 0, 0),
            new(Carol, 3, 1, 2),
        };

        return (members, predictions, results);
    }

    private static WorldCup.Api.DTOs.AwardEntry Award(WorldCup.Api.DTOs.LeagueStatsResponse stats, string key) =>
        stats.PersonalAwards.Single(a => a.Key == key);

    [Fact]
    public void Calculate_ReportsMemberAndMatchCounts()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        stats.MemberCount.Should().Be(3);
        stats.ScoredMatchCount.Should().Be(3);
    }

    [Fact]
    public void Nostradamus_GoesToMostExactScorelines()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        var award = Award(stats, "nostradamus");
        award.WinnerName.Should().Be("Carol");
        award.Value.Should().Be(2);
    }

    [Fact]
    public void Utfallsekspert_GoesToMostOutcomeHits()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        var award = Award(stats, "utfallsekspert");
        award.WinnerName.Should().Be("Alice");
        award.Value.Should().Be(3);
    }

    [Fact]
    public void LengsteRekke_GoesToLongestPointStreak()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        var award = Award(stats, "lengste_rekke");
        award.WinnerName.Should().Be("Alice");
        award.Value.Should().Be(3);
    }

    [Fact]
    public void Brannfakkel_GoesToWorstSingleOutcomeMiss()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        var award = Award(stats, "brannfakkel");
        // Bob (kamp 3) og Carol (kamp 1) bommer begge med severity 4; laveste MatchId vinner.
        award.WinnerName.Should().Be("Carol");
        award.MatchId.Should().Be(1);
        award.Value.Should().Be(4);
    }

    [Fact]
    public void Presisjon_GoesToHighestGoalBonusRatio()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        var award = Award(stats, "presisjon");
        award.WinnerName.Should().Be("Carol");
        award.Value.Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    public void OptimistAndGjerrigknark_RankByPredictedGoals()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        Award(stats, "optimist").WinnerName.Should().Be("Bob");

        var gjerrig = Award(stats, "gjerrigknark");
        gjerrig.WinnerName.Should().Be("Alice"); // Alice og Carol snitt 2.0; navn avgjør likt
        gjerrig.Value.Should().BeApproximately(2.0, 0.0001);
    }

    [Fact]
    public void MatchFacts_IdentifyHardestEasiestAndShock()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        stats.MatchFacts.HardestMatch!.MatchId.Should().Be(1);
        stats.MatchFacts.EasiestMatch!.MatchId.Should().Be(2);
        stats.MatchFacts.EasiestMatch.Value.Should().BeApproximately(2.667, 0.001);
        stats.MatchFacts.BiggestShock!.MatchId.Should().Be(1);
    }

    [Fact]
    public void Aggregate_ComputesGoalAveragesAndStageSplit()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        stats.Aggregate.AvgActualGoalsPerMatch.Should().BeApproximately(2.0, 0.0001);
        stats.Aggregate.AvgPredictedGoalsPerMatch.Should().BeApproximately(20.0 / 9.0, 0.0001);
        stats.Aggregate.GroupStage.OutcomeHitRate.Should().BeApproximately(5.0 / 6.0, 0.0001);
        stats.Aggregate.Knockout.AvgPoints.Should().BeApproximately(2.0, 0.0001);
    }

    [Fact]
    public void Drama_ReportsBiggestClimbAndFall()
    {
        var (members, predictions, results) = BuildScenario();

        var stats = _calculator.Calculate(members, predictions, results, MatchInfos);

        stats.Drama.BiggestClimb!.Name.Should().Be("Carol");
        stats.Drama.BiggestClimb.Positions.Should().Be(2);
        stats.Drama.BiggestFall!.Name.Should().Be("Bob");
        stats.Drama.BiggestFall.Positions.Should().Be(1);
        stats.Drama.LeadChanges.Should().Be(0); // Alice leder hele veien
    }

    [Fact]
    public void LeadChanges_CountsWhenTopSpotSwitches()
    {
        // Zoe leder etter kamp 1; etter kamp 2 er de like på poeng og navnet avgjør (Aaron < Zoe).
        var zoe = Guid.NewGuid();
        var aaron = Guid.NewGuid();
        var members = new List<StatMember> { new(zoe, "Zoe", null), new(aaron, "Aaron", null) };
        var t0 = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc);
        var results = new List<StatResult> { new(1, 1, 0, t0), new(2, 1, 0, t0.AddHours(3)) };
        var predictions = new List<StatPrediction>
        {
            new(zoe, 1, 1, 0),   // blinkskudd -> 4
            new(zoe, 2, 0, 1),   // bom -> 0
            new(aaron, 1, 0, 1), // bom -> 0
            new(aaron, 2, 1, 0), // blinkskudd -> 4
        };
        var matchInfos = new List<StatMatchInfo>
        {
            new(1, "group-1", t0, true),
            new(2, "group-1", t0.AddHours(3), true),
        };

        var stats = _calculator.Calculate(members, predictions, results, matchInfos);

        stats.Drama.LeadChanges.Should().Be(1);
    }

    [Fact]
    public void PopularScoreline_PicksMostFrequentPrediction()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var members = new List<StatMember> { new(a, "A", null), new(b, "B", null) };
        var results = new List<StatResult>();
        var predictions = new List<StatPrediction>
        {
            new(a, 1, 2, 1),
            new(b, 1, 2, 1),
            new(a, 2, 0, 0),
        };

        var stats = _calculator.Calculate(members, predictions, results, new List<StatMatchInfo>());

        stats.MatchFacts.PopularScoreline!.HomeScore.Should().Be(2);
        stats.MatchFacts.PopularScoreline.AwayScore.Should().Be(1);
        stats.MatchFacts.PopularScoreline.Count.Should().Be(2);
    }

    [Fact]
    public void Calculate_WithNoData_ReturnsEmptyResultWithoutThrowing()
    {
        var stats = _calculator.Calculate([], [], [], new List<StatMatchInfo>());

        stats.MemberCount.Should().Be(0);
        stats.ScoredMatchCount.Should().Be(0);
        stats.PersonalAwards.Should().BeEmpty();
        stats.MatchFacts.HardestMatch.Should().BeNull();
        stats.MatchFacts.PopularScoreline.Should().BeNull();
        stats.Drama.BiggestClimb.Should().BeNull();
    }

    [Fact]
    public void Participation_CountsMembersPerLockedMatchInChronologicalOrder()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var members = new List<StatMember> { new(a, "A", null), new(b, "B", null), new(c, "C", null) };
        var t0 = new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc);
        var results = new List<StatResult> { new(1, 1, 0, t0), new(2, 1, 0, t0.AddHours(3)) };
        var predictions = new List<StatPrediction>
        {
            // Kamp 1: alle tre tipper. Kamp 2: C har falt fra. Kamp 3 (ulåst): kun A – skal ikke telle.
            new(a, 1, 1, 0), new(b, 1, 1, 0), new(c, 1, 0, 1),
            new(a, 2, 1, 0), new(b, 2, 1, 0),
            new(a, 3, 2, 2),
        };
        var matchInfos = new List<StatMatchInfo>
        {
            new(1, "group-1", t0, true),
            new(2, "group-1", t0.AddHours(3), true),
            new(3, "group-2", t0.AddDays(5), false), // ikke startet ennå
        };

        var stats = _calculator.Calculate(members, predictions, results, matchInfos);

        stats.Participation.Should().HaveCount(2);
        stats.Participation[0].MatchId.Should().Be(1);
        stats.Participation[0].Count.Should().Be(3);
        stats.Participation[1].MatchId.Should().Be(2);
        stats.Participation[1].Count.Should().Be(2);
    }
}
