namespace WorldCup.Api.Models;

/// <summary>
/// Tracks pending result fetches for matches we've already attempted but haven't yet
/// received a result for. Used to implement bounded retries with backoff so we don't
/// burn the daily API budget polling for a single match.
/// </summary>
public class PendingMatchFetch
{
    public int MatchId { get; set; }

    /// <summary>When the match was first expected to be ready (kickoff + stage buffer).</summary>
    public DateTime FirstScheduledAt { get; set; }

    /// <summary>When the next fetch attempt should occur.</summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>How many fetch attempts have been made so far.</summary>
    public int AttemptCount { get; set; }
}
