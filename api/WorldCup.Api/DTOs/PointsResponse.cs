namespace WorldCup.Api.DTOs;

public class PointsResponse
{
    public int MatchId { get; set; }
    public int Points { get; set; }
    public int OutcomePoints { get; set; }
    public int HomeGoalPoints { get; set; }
    public int AwayGoalPoints { get; set; }
}
