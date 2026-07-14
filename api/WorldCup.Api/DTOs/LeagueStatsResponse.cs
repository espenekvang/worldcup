namespace WorldCup.Api.DTOs;

/// <summary>
/// Samlet «VM-oppsummering» for én liga. Alle tall er regnet ut over ligaens medlemmer
/// (predictions er globale per bruker, men her scopes utregningen til gruppens medlemmer).
/// Backend returnerer rå tall/nøkler; frontend eier all norsk tekst og formatering.
/// </summary>
public class LeagueStatsResponse
{
    /// <summary>Antall kamper som har et registrert resultat (grunnlaget for all utregning).</summary>
    public int ScoredMatchCount { get; set; }

    /// <summary>Antall medlemmer i ligaen.</summary>
    public int MemberCount { get; set; }

    /// <summary>Personlige priser – én vinner kåres blant medlemmene per pris.</summary>
    public List<AwardEntry> PersonalAwards { get; set; } = [];

    public MatchFacts MatchFacts { get; set; } = new();

    public AggregateStats Aggregate { get; set; } = new();

    public DramaStats Drama { get; set; } = new();
}

/// <summary>
/// En personlig pris. <see cref="Key"/> er en stabil id som frontend mapper til
/// tittel/emoji/beskrivelse. <see cref="Value"/> er den primære metrikken (frontend
/// formaterer den). <see cref="WinnerName"/> er null når prisen ikke kan kåres (for lite data).
/// </summary>
public class AwardEntry
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Fullt navn på vinneren (frontend anvender ligaens navnevisning). Null når ingen vinner.</summary>
    public string? WinnerName { get; set; }

    public string? WinnerPicture { get; set; }

    /// <summary>Primær metrikk for prisen (f.eks. antall blinkskudd, snittpoeng, andel).</summary>
    public double Value { get; set; }

    /// <summary>Satt for priser som er knyttet til én bestemt kamp (f.eks. «Årets brannfakkel»).</summary>
    public int? MatchId { get; set; }
}

public class MatchFacts
{
    /// <summary>Kampen med lavest snittpoeng blant medlemmene.</summary>
    public MatchStat? HardestMatch { get; set; }

    /// <summary>Kampen med høyest snittpoeng blant medlemmene.</summary>
    public MatchStat? EasiestMatch { get; set; }

    /// <summary>Kampen der færrest traff utfallet (H/U/B).</summary>
    public MatchStat? BiggestShock { get; set; }

    /// <summary>Mest tippede resultat på tvers av alle medlemmenes tips.</summary>
    public PopularScoreline? PopularScoreline { get; set; }
}

public class MatchStat
{
    public int MatchId { get; set; }

    /// <summary>Snittpoeng (Hardest/Easiest) eller andel som traff utfallet 0..1 (BiggestShock).</summary>
    public double Value { get; set; }
}

public class PopularScoreline
{
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    /// <summary>Hvor mange tips som hadde nettopp dette resultatet.</summary>
    public int Count { get; set; }
}

public class AggregateStats
{
    /// <summary>Gjennomsnittlig antall mål medlemmene tippet per kamp.</summary>
    public double AvgPredictedGoalsPerMatch { get; set; }

    /// <summary>Faktisk gjennomsnittlig antall mål per spilte kamp.</summary>
    public double AvgActualGoalsPerMatch { get; set; }

    public StageAccuracy GroupStage { get; set; } = new();

    public StageAccuracy Knockout { get; set; } = new();
}

public class StageAccuracy
{
    /// <summary>Antall scorede tips i denne fasen.</summary>
    public int PredictionCount { get; set; }

    /// <summary>Snittpoeng per tips i fasen.</summary>
    public double AvgPoints { get; set; }

    /// <summary>Andel tips som traff utfallet (0..1).</summary>
    public double OutcomeHitRate { get; set; }
}

public class DramaStats
{
    /// <summary>Antall ganger ledelsen i ligaen skiftet gjennom turneringen.</summary>
    public int LeadChanges { get; set; }

    /// <summary>Størst klatring på tavlen (fra dårligste plassering til slutt-plassering).</summary>
    public RankMovement? BiggestClimb { get; set; }

    /// <summary>Størst fall på tavlen (fra beste plassering til slutt-plassering).</summary>
    public RankMovement? BiggestFall { get; set; }
}

public class RankMovement
{
    public string? Name { get; set; }
    public string? Picture { get; set; }

    /// <summary>Antall plasseringer klatret/falt.</summary>
    public int Positions { get; set; }
}
