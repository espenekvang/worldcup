namespace WorldCup.Api.DTOs;

public class LeaderboardEntry
{
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public int TotalPoints { get; set; }
    public int MatchCount { get; set; }
}
