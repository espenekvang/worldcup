using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldCup.Api.Services;

public sealed class MatchSchedule
{
    private readonly Dictionary<int, MatchEntry> _matchesById;
    private readonly Dictionary<string, DateTime> _earliestKickoffByStage;

    public MatchSchedule(IReadOnlyList<MatchEntry> matches)
    {
        _matchesById = matches.ToDictionary(m => m.Id);
        _earliestKickoffByStage = matches
            .GroupBy(m => m.Stage)
            .ToDictionary(g => g.Key, g => g.Min(m => m.Date));
    }

    public MatchEntry? GetMatch(int matchId) =>
        _matchesById.GetValueOrDefault(matchId);

    public IReadOnlyList<MatchEntry> GetAllMatches() => _matchesById.Values.ToList();

    public bool IsStageLocked(string stage) =>
        _earliestKickoffByStage.TryGetValue(stage, out var earliest) && DateTime.UtcNow >= earliest;

    public bool IsMatchLocked(int matchId) =>
        _matchesById.TryGetValue(matchId, out var match) && DateTime.UtcNow >= match.Date;

    public static MatchSchedule LoadFromJson(string path)
    {
        var json = File.ReadAllText(path);
        var matches = JsonSerializer.Deserialize<List<MatchEntry>>(json)
            ?? throw new InvalidOperationException("Failed to parse matches.json");
        return new MatchSchedule(matches);
    }
}

public sealed class MatchEntry
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("date")]
    public DateTime Date { get; init; }

    [JsonPropertyName("stage")]
    public string Stage { get; init; } = string.Empty;

    [JsonPropertyName("homeTeam")]
    public string? HomeTeam { get; init; }

    [JsonPropertyName("awayTeam")]
    public string? AwayTeam { get; init; }

    [JsonPropertyName("homePlaceholder")]
    public string? HomePlaceholder { get; init; }

    [JsonPropertyName("awayPlaceholder")]
    public string? AwayPlaceholder { get; init; }

    [JsonPropertyName("group")]
    public string? Group { get; init; }

    [JsonPropertyName("venueId")]
    public string VenueId { get; init; } = string.Empty;

    [JsonPropertyName("manualOverride")]
    public bool ManualOverride { get; init; }

    public bool AreTeamsUndetermined => HomeTeam is null || AwayTeam is null;

    /// <summary>
    /// True when both proposed team codes are known and identical — an impossible fixture, since
    /// a team can never play itself. Auto-fill of knockout teams maps an upstream record to a
    /// local fixture heuristically (by match number with a kickoff-time fallback) and writes the
    /// home/away slots independently, so a mis-mapped or duplicated upstream record can otherwise
    /// land the same team on both sides (e.g. "Paraguay vs Paraguay"). Fill sites use this to
    /// reject such writes and leave the fixture unresolved for the next poll.
    /// </summary>
    public static bool WouldDuplicateTeam(string? home, string? away) =>
        !string.IsNullOrWhiteSpace(home)
        && string.Equals(home, away, StringComparison.OrdinalIgnoreCase);
}
