namespace WorldCup.Api.DTOs;

public class InvitationResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid BettingGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
