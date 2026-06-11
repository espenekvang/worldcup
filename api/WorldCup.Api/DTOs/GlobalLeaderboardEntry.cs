namespace WorldCup.Api.DTOs;

public class GlobalLeaderboardEntry
{
    public string? Name { get; set; }

    /// <summary>Selvvalgt visningsnavn. Null/tom = bruk <see cref="Name"/>.</summary>
    public string? DisplayName { get; set; }

    public string? Picture { get; set; }
    public int TotalPoints { get; set; }
    public int MatchCount { get; set; }
    public bool IsInCurrentGroup { get; set; }
    public string? GroupName { get; set; }
}
