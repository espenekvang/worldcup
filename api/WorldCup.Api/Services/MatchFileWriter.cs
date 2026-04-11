using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WorldCup.Api.Services;

public sealed class MatchFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MatchScheduleProvider _scheduleProvider;
    private readonly ILogger<MatchFileWriter> _logger;
    private readonly string _jsonPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public MatchFileWriter(
        MatchScheduleProvider scheduleProvider,
        ILogger<MatchFileWriter> logger,
        IOptions<MatchFileWriterOptions> options)
    {
        _scheduleProvider = scheduleProvider;
        _logger = logger;
        _jsonPath = options.Value.JsonPath;

        EnsureSeeded();
    }

    public async Task WriteAsync(IReadOnlyList<MatchEntry> matches, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);

        try
        {
            var tmpPath = $"{_jsonPath}.tmp";
            var json = JsonSerializer.Serialize(matches, JsonOptions);

            await File.WriteAllTextAsync(tmpPath, json, ct);
            File.Move(tmpPath, _jsonPath, overwrite: true);

            _scheduleProvider.Reload(matches);
            _logger.LogInformation("Wrote {Count} matches to {Path}", matches.Count, _jsonPath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void EnsureSeeded()
    {
        if (File.Exists(_jsonPath))
        {
            return;
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "data", "matches.json");
        if (!File.Exists(seedPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_jsonPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(seedPath, _jsonPath, overwrite: true);
        _logger.LogWarning("Seeded matches file at {Path} from {SeedPath}", _jsonPath, seedPath);
    }
}

public sealed class MatchFileWriterOptions
{
    public string JsonPath { get; init; } = string.Empty;
}
