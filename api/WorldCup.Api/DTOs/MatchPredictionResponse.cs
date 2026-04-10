namespace WorldCup.Api.DTOs;

public class MatchPredictionResponse
{
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
}
