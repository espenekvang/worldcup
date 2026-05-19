namespace WorldCup.Api.DTOs;

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
    public string? InviteToken { get; set; }
}
