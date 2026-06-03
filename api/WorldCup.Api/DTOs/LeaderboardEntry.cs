namespace WorldCup.Api.DTOs;

public class LeaderboardEntry
{
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public int TotalPoints { get; set; }
    public int MatchCount { get; set; }

    /// <summary>
    /// Plassering før siste registrerte kamp. Null hvis ingen tidligere kamp finnes
    /// (dvs. dette er den første kampen med resultat).
    /// </summary>
    public int? PreviousRank { get; set; }
    public bool HasPaid { get; set; }
}
