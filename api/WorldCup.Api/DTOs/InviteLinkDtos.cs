namespace WorldCup.Api.DTOs;

public class InviteLinkResponse
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
}

public class InviteLinkInfoResponse
{
    public string GroupName { get; set; } = string.Empty;
    public Guid BettingGroupId { get; set; }
}

public class AcceptInviteLinkResponse
{
    public Guid BettingGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public bool AlreadyMember { get; set; }
}
