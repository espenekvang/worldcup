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

    /// <summary>
    /// Valgfri overstyring av avsendernavn ved visning. Brukes for systemmeldinger
    /// (f.eks. Dommeren) der vi vil vise et kampspesifikt navn i stedet for
    /// brukerens lagrede navn.
    /// </summary>
    public string? SenderDisplayNameOverride { get; set; }

    public BettingGroup BettingGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
