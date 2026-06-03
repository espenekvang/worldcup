namespace WorldCup.Api.Models;

public class ChatMessageReaction
{
    public Guid Id { get; set; }
    public Guid ChatMessageId { get; set; }
    public Guid UserId { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatMessage ChatMessage { get; set; } = null!;
    public User User { get; set; } = null!;
}
