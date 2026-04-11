namespace WorldCup.Api.Models;

public class MatchResult
{
    public Guid Id { get; set; }
    public int MatchId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
