namespace WorldCup.Api.DTOs;

public class MatchPredictionResponse
{
    public string? Name { get; set; } = null;

    /// <summary>Selvvalgt visningsnavn. Null/tom = bruk <see cref="Name"/>.</summary>
    public string? DisplayName { get; set; }

    public string? Picture { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public int? Points { get; set; }
}
