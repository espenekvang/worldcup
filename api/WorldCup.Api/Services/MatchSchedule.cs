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
}
