namespace WorldCup.Api.Models;

public class Prediction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int MatchId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int? Points { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid BettingGroupId { get; set; }

    public User User { get; set; } = null!;
    public BettingGroup BettingGroup { get; set; } = null!;
}
