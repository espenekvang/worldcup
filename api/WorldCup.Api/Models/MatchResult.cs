namespace WorldCup.Api.Models;

public class MatchResult
{
    public Guid Id { get; set; }
    public int MatchId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navn p\u00e5 dommer for kampen, hvis tilgjengelig fra oppstr\u00f8ms-API. Brukes f.eks.
    /// som avsendernavn for Dommeren-meldingen i liga-chatten.
    /// </summary>
    public string? Referee { get; set; }
}
