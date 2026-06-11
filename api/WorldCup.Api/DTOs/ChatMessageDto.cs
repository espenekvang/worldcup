namespace WorldCup.Api.DTOs;

public class ChatMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class BroadcastMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class ChatReactionSummary
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool ReactedByMe { get; set; }
}

public class ChatMessageResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Avsenderens selvvalgte visningsnavn. Når satt vises dette i sin helhet
    /// (ikke avkortet til fornavn). Null for system-/kringkastingsmeldinger.
    /// </summary>
    public string? UserDisplayName { get; set; }

    public string? UserPicture { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsSystem { get; set; }
    public List<ChatReactionSummary> Reactions { get; set; } = [];
}

public class ChatDeletedEventDto
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
}

public class ChatReactionEventDto
{
    public Guid MessageId { get; set; }
    public Guid BettingGroupId { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ChatUnreadCountResponse
{
    public int UnreadCount { get; set; }
}
