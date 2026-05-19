namespace WorldCup.Api.Models;

public class BettingGroupInviteLink
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }

    public BettingGroup BettingGroup { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
