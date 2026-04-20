namespace WorldCup.Api.DTOs;

public class InvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public Guid BettingGroupId { get; set; }
}
