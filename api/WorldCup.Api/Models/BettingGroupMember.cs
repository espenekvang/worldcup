namespace WorldCup.Api.Models;

public class BettingGroupMember
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public BettingGroup BettingGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
