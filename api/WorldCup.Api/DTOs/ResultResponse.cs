namespace WorldCup.Api.DTOs;

public class ResultResponse
{
    public int MatchId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public DateTime FetchedAt { get; set; }
}
