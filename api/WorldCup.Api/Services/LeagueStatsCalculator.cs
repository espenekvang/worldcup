using WorldCup.Api.DTOs;

namespace WorldCup.Api.Services;

/// <summary>Ett medlem i ligaen (input til statistikk-utregningen).</summary>
public sealed record StatMember(Guid UserId, string Name, string? Picture);

/// <summary>Ett tips (input). Predictions er globale per bruker; kalleren filtrerer til ligaens medlemmer.</summary>
public sealed record StatPrediction(Guid UserId, int MatchId, int HomeScore, int AwayScore);

/// <summary>Ett kampresultat (input).</summary>
public sealed record StatResult(int MatchId, int HomeScore, int AwayScore, DateTime FetchedAt);

/// <summary>
/// Regner ut «VM-oppsummeringen» for én liga fra rene lister. Holdt fri for EF/DbContext slik at
/// den kan enhetstestes uten database (på samme måte som <see cref="ScoringService"/>).
/// </summary>
public sealed class LeagueStatsCalculator(ScoringService scoringService)
{
    /// <summary>Minste antall scorede tips for å kvalifisere til snitt-/andelsbaserte priser.</summary>
    private const int RateEligibilityThreshold = 5;

    public LeagueStatsResponse Calculate(
        IReadOnlyList<StatMember> members,
        IReadOnlyList<StatPrediction> predictions,
        IReadOnlyList<StatResult> results,
        IReadOnlyDictionary<int, string> matchStages)
    {
        var response = new LeagueStatsResponse
        {
            ScoredMatchCount = results.Count,
            MemberCount = members.Count,
        };

        var memberIds = members.Select(m => m.UserId).ToHashSet();
        var resultByMatch = results
            .GroupBy(r => r.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        // Alle tips fra medlemmene (også på ikke-spilte kamper) – brukes til «folkets resultat».
        var memberPredictions = predictions.Where(p => memberIds.Contains(p.UserId)).ToList();

        // Scorede tips: medlemmenes tips på kamper som har resultat, beriket med poeng.
        var scored = new List<ScoredPrediction>();
        foreach (var p in memberPredictions)
        {
            if (!resultByMatch.TryGetValue(p.MatchId, out var r))
            {
                continue;
            }

            var points = scoringService.CalculatePoints(p.HomeScore, p.AwayScore, r.HomeScore, r.AwayScore);
            scored.Add(new ScoredPrediction(
                UserId: p.UserId,
                MatchId: p.MatchId,
                PredHome: p.HomeScore,
                PredAway: p.AwayScore,
                ActHome: r.HomeScore,
                ActAway: r.AwayScore,
                Points: points));
        }

        // Kronologisk rekkefølge på spilte kamper (nyeste resultat sist), for streaks og drama.
        var chronologicalMatchIds = results
            .OrderBy(r => r.FetchedAt)
            .ThenBy(r => r.MatchId)
            .Select(r => r.MatchId)
            .ToList();

        response.PersonalAwards = BuildPersonalAwards(members, scored, chronologicalMatchIds);
        response.MatchFacts = BuildMatchFacts(scored, memberPredictions);
        response.Aggregate = BuildAggregate(scored, results, matchStages);
        response.Drama = BuildDrama(members, scored, chronologicalMatchIds);

        return response;
    }

    private List<AwardEntry> BuildPersonalAwards(
        IReadOnlyList<StatMember> members,
        List<ScoredPrediction> scored,
        List<int> chronologicalMatchIds)
    {
        var byUser = scored.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var aggs = new List<MemberAgg>();
        foreach (var member in members)
        {
            if (!byUser.TryGetValue(member.UserId, out var preds) || preds.Count == 0)
            {
                continue;
            }

            var agg = new MemberAgg { Member = member };
            foreach (var s in preds)
            {
                agg.Scored++;
                agg.TotalPoints += s.Points;
                if (s.IsExact) agg.ExactCount++;
                if (s.OutcomeHit) agg.OutcomeHits++;
                if (s.PredHome == s.PredAway) agg.DrawPredictions++;
                agg.PredictedGoalsSum += s.PredHome + s.PredAway;
                agg.BonusPoints += s.GoalBonus;
            }

            // Streaks i kronologisk kamprekkefølge.
            var ordered = chronologicalMatchIds
                .Select(mid => preds.FirstOrDefault(p => p.MatchId == mid))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();

            int pointRun = 0, dryRun = 0;
            foreach (var s in ordered)
            {
                if (s.Points >= 1)
                {
                    pointRun++;
                    dryRun = 0;
                }
                else
                {
                    dryRun++;
                    pointRun = 0;
                }
                agg.LongestPointStreak = Math.Max(agg.LongestPointStreak, pointRun);
                agg.LongestDryStreak = Math.Max(agg.LongestDryStreak, dryRun);
            }

            aggs.Add(agg);
        }

        var awards = new List<AwardEntry>();
        if (aggs.Count == 0)
        {
            return awards;
        }

        var maxScored = aggs.Max(a => a.Scored);
        var rateThreshold = Math.Min(RateEligibilityThreshold, maxScored);
        var rateEligible = aggs.Where(a => a.Scored >= rateThreshold).ToList();

        // Antallsbaserte priser (kun vinner hvis metrikken er > 0).
        awards.Add(MaxAward("nostradamus", aggs, a => a.ExactCount, requirePositive: true));
        awards.Add(MaxAward("utfallsekspert", aggs, a => a.OutcomeHits, requirePositive: true));
        awards.Add(MaxAward("kaptein_uavgjort", aggs, a => a.DrawPredictions, requirePositive: true));
        awards.Add(MaxAward("lengste_rekke", aggs, a => a.LongestPointStreak, requirePositive: true));
        awards.Add(MaxAward("lengste_torke", aggs, a => a.LongestDryStreak, requirePositive: true));

        // Snitt-/andelsbaserte priser (krever et minste antall tips).
        awards.Add(MaxAward("presisjon", rateEligible, a => a.BonusRatio, requirePositive: true));
        awards.Add(MaxAward("best_snitt", rateEligible, a => a.AvgPoints, requirePositive: false));
        awards.Add(MaxAward("optimist", rateEligible, a => a.AvgPredictedGoals, requirePositive: false));
        awards.Add(MinAward("gjerrigknark", rateEligible, a => a.AvgPredictedGoals));

        // Årets brannfakkel: den enkeltstående verste utfallsbommen i hele ligaen.
        var worst = scored
            .Where(s => !s.OutcomeHit)
            .OrderByDescending(s => s.OutcomeMissSeverity)
            .ThenBy(s => s.MatchId)
            .FirstOrDefault();
        if (worst is not null)
        {
            var member = members.First(m => m.UserId == worst.UserId);
            awards.Add(new AwardEntry
            {
                Key = "brannfakkel",
                WinnerName = member.Name,
                WinnerPicture = member.Picture,
                Value = worst.OutcomeMissSeverity,
                MatchId = worst.MatchId,
            });
        }
        else
        {
            awards.Add(new AwardEntry { Key = "brannfakkel" });
        }

        return awards;
    }

    private static AwardEntry MaxAward(string key, IReadOnlyList<MemberAgg> pool, Func<MemberAgg, double> selector, bool requirePositive)
    {
        var winner = pool
            .OrderByDescending(selector)
            .ThenBy(a => a.Member.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (winner is null || (requirePositive && selector(winner) <= 0))
        {
            return new AwardEntry { Key = key };
        }

        return new AwardEntry
        {
            Key = key,
            WinnerName = winner.Member.Name,
            WinnerPicture = winner.Member.Picture,
            Value = selector(winner),
        };
    }

    private static AwardEntry MinAward(string key, IReadOnlyList<MemberAgg> pool, Func<MemberAgg, double> selector)
    {
        var winner = pool
            .OrderBy(selector)
            .ThenBy(a => a.Member.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (winner is null)
        {
            return new AwardEntry { Key = key };
        }

        return new AwardEntry
        {
            Key = key,
            WinnerName = winner.Member.Name,
            WinnerPicture = winner.Member.Picture,
            Value = selector(winner),
        };
    }

    private static MatchFacts BuildMatchFacts(List<ScoredPrediction> scored, List<StatPrediction> memberPredictions)
    {
        var facts = new MatchFacts();

        var perMatch = scored
            .GroupBy(s => s.MatchId)
            .Select(g => new
            {
                MatchId = g.Key,
                AvgPoints = g.Average(s => s.Points),
                OutcomeRate = g.Count(s => s.OutcomeHit) / (double)g.Count(),
            })
            .ToList();

        if (perMatch.Count > 0)
        {
            var hardest = perMatch.OrderBy(m => m.AvgPoints).ThenBy(m => m.MatchId).First();
            facts.HardestMatch = new MatchStat { MatchId = hardest.MatchId, Value = hardest.AvgPoints };

            var easiest = perMatch.OrderByDescending(m => m.AvgPoints).ThenBy(m => m.MatchId).First();
            facts.EasiestMatch = new MatchStat { MatchId = easiest.MatchId, Value = easiest.AvgPoints };

            var shock = perMatch.OrderBy(m => m.OutcomeRate).ThenBy(m => m.MatchId).First();
            facts.BiggestShock = new MatchStat { MatchId = shock.MatchId, Value = shock.OutcomeRate };
        }

        // Folkets resultat: mest tippede scoreline over alle medlemmenes tips.
        var popular = memberPredictions
            .GroupBy(p => (p.HomeScore, p.AwayScore))
            .Select(g => new { g.Key.HomeScore, g.Key.AwayScore, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.HomeScore)
            .ThenBy(x => x.AwayScore)
            .FirstOrDefault();
        if (popular is not null)
        {
            facts.PopularScoreline = new PopularScoreline
            {
                HomeScore = popular.HomeScore,
                AwayScore = popular.AwayScore,
                Count = popular.Count,
            };
        }

        return facts;
    }

    private static AggregateStats BuildAggregate(
        List<ScoredPrediction> scored,
        IReadOnlyList<StatResult> results,
        IReadOnlyDictionary<int, string> matchStages)
    {
        var aggregate = new AggregateStats();

        if (scored.Count > 0)
        {
            aggregate.AvgPredictedGoalsPerMatch = scored.Average(s => s.PredHome + s.PredAway);
        }

        if (results.Count > 0)
        {
            aggregate.AvgActualGoalsPerMatch = results.Average(r => r.HomeScore + r.AwayScore);
        }

        aggregate.GroupStage = BuildStageAccuracy(scored, matchStages, isGroupStage: true);
        aggregate.Knockout = BuildStageAccuracy(scored, matchStages, isGroupStage: false);

        return aggregate;
    }

    private static StageAccuracy BuildStageAccuracy(
        List<ScoredPrediction> scored,
        IReadOnlyDictionary<int, string> matchStages,
        bool isGroupStage)
    {
        var bucket = scored
            .Where(s => IsGroupStage(matchStages, s.MatchId) == isGroupStage)
            .ToList();

        if (bucket.Count == 0)
        {
            return new StageAccuracy();
        }

        return new StageAccuracy
        {
            PredictionCount = bucket.Count,
            AvgPoints = bucket.Average(s => s.Points),
            OutcomeHitRate = bucket.Count(s => s.OutcomeHit) / (double)bucket.Count,
        };
    }

    private static bool IsGroupStage(IReadOnlyDictionary<int, string> matchStages, int matchId) =>
        matchStages.TryGetValue(matchId, out var stage)
        && stage.StartsWith("group", StringComparison.OrdinalIgnoreCase);

    private static DramaStats BuildDrama(
        IReadOnlyList<StatMember> members,
        List<ScoredPrediction> scored,
        List<int> chronologicalMatchIds)
    {
        var drama = new DramaStats();
        if (members.Count == 0 || chronologicalMatchIds.Count == 0)
        {
            return drama;
        }

        // Poeng per medlem per kamp, for raske oppslag underveis.
        var pointsByUserMatch = scored
            .GroupBy(s => (s.UserId, s.MatchId))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Points));

        var cumulative = members.ToDictionary(m => m.UserId, _ => 0);
        var bestRank = members.ToDictionary(m => m.UserId, _ => int.MaxValue);
        var worstRank = members.ToDictionary(m => m.UserId, _ => int.MinValue);
        Dictionary<Guid, int> finalRanks = new();

        Guid? previousLeader = null;
        var leadChanges = 0;

        foreach (var matchId in chronologicalMatchIds)
        {
            foreach (var member in members)
            {
                if (pointsByUserMatch.TryGetValue((member.UserId, matchId), out var pts))
                {
                    cumulative[member.UserId] += pts;
                }
            }

            var ranks = ComputeRanks(members, cumulative);
            finalRanks = ranks;

            foreach (var (userId, rank) in ranks)
            {
                bestRank[userId] = Math.Min(bestRank[userId], rank);
                worstRank[userId] = Math.Max(worstRank[userId], rank);
            }

            var leader = members
                .OrderByDescending(m => cumulative[m.UserId])
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .First().UserId;

            if (previousLeader is not null && previousLeader != leader)
            {
                leadChanges++;
            }
            previousLeader = leader;
        }

        drama.LeadChanges = leadChanges;

        // Klatring: dårligste plassering underveis → slutt-plassering (positivt = klatret oppover).
        var climb = members
            .Select(m => new { m, Positions = worstRank[m.UserId] - finalRanks[m.UserId] })
            .Where(x => x.Positions > 0)
            .OrderByDescending(x => x.Positions)
            .ThenBy(x => x.m.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (climb is not null)
        {
            drama.BiggestClimb = new RankMovement { Name = climb.m.Name, Picture = climb.m.Picture, Positions = climb.Positions };
        }

        // Fall: beste plassering underveis → slutt-plassering (positivt = falt nedover).
        var fall = members
            .Select(m => new { m, Positions = finalRanks[m.UserId] - bestRank[m.UserId] })
            .Where(x => x.Positions > 0)
            .OrderByDescending(x => x.Positions)
            .ThenBy(x => x.m.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (fall is not null)
        {
            drama.BiggestFall = new RankMovement { Name = fall.m.Name, Picture = fall.m.Picture, Positions = fall.Positions };
        }

        return drama;
    }

    /// <summary>Standard competition ranking (1-2-2-4): lik poengsum deler plassering.</summary>
    private static Dictionary<Guid, int> ComputeRanks(IReadOnlyList<StatMember> members, IReadOnlyDictionary<Guid, int> points)
    {
        var ordered = members
            .OrderByDescending(m => points[m.UserId])
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ranks = new Dictionary<Guid, int>();
        int rank = 0;
        int? lastPoints = null;
        for (int i = 0; i < ordered.Count; i++)
        {
            var pts = points[ordered[i].UserId];
            if (lastPoints is null || pts != lastPoints)
            {
                rank = i + 1;
                lastPoints = pts;
            }
            ranks[ordered[i].UserId] = rank;
        }

        return ranks;
    }

    private sealed record ScoredPrediction(
        Guid UserId,
        int MatchId,
        int PredHome,
        int PredAway,
        int ActHome,
        int ActAway,
        int Points)
    {
        public bool IsExact => PredHome == ActHome && PredAway == ActAway;

        public bool OutcomeHit => Math.Sign(PredHome - PredAway) == Math.Sign(ActHome - ActAway);

        /// <summary>Bonuspoeng for eksakt antall mål (0–2), uavhengig av utfallspoeng.</summary>
        public int GoalBonus => (PredHome == ActHome ? 1 : 0) + (PredAway == ActAway ? 1 : 0);

        /// <summary>Hvor «feil» en utfallsbom var: avstand mellom tippet og faktisk måldifferanse.</summary>
        public int OutcomeMissSeverity => Math.Abs((PredHome - PredAway) - (ActHome - ActAway));
    }

    private sealed class MemberAgg
    {
        public StatMember Member { get; set; } = null!;
        public int Scored { get; set; }
        public int TotalPoints { get; set; }
        public int ExactCount { get; set; }
        public int OutcomeHits { get; set; }
        public int DrawPredictions { get; set; }
        public int PredictedGoalsSum { get; set; }
        public int BonusPoints { get; set; }
        public int LongestPointStreak { get; set; }
        public int LongestDryStreak { get; set; }

        public double AvgPoints => Scored > 0 ? (double)TotalPoints / Scored : 0;
        public double AvgPredictedGoals => Scored > 0 ? (double)PredictedGoalsSum / Scored : 0;
        public double BonusRatio => TotalPoints > 0 ? (double)BonusPoints / TotalPoints : 0;
    }
}
