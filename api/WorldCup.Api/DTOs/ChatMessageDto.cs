namespace WorldCup.Api.DTOs;

public class ChatMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class ChatMessageResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserPicture { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class ChatDeletedEventDto
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
}

public class ChatUnreadCountResponse
{
    public int UnreadCount { get; set; }
}
