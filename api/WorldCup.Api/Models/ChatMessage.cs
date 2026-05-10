namespace WorldCup.Api.Models;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public BettingGroup BettingGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
