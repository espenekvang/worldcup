using System.Threading;
using System.Text.Json;

namespace WorldCup.Api.Services;

public sealed class MatchScheduleProvider
{
    private volatile MatchSchedule _current;

    public MatchScheduleProvider(string jsonPath)
    {
        _current = MatchSchedule.LoadFromJson(jsonPath);
    }

    public MatchSchedule Current => _current;

    public void Reload(IReadOnlyList<MatchEntry> matches)
    {
        Interlocked.Exchange(ref _current, new MatchSchedule(matches));
    }

    public void Reload(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var matches = JsonSerializer.Deserialize<List<MatchEntry>>(json)
            ?? throw new InvalidOperationException("Failed to parse matches.json");

        Reload(matches);
    }
}
