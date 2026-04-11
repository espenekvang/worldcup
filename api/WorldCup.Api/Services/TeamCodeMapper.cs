using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldCup.Api.Services;

public sealed class TeamCodeMapper
{
    private static readonly Dictionary<string, string> ManualOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Algeria"] = "ALG",
        ["Austria"] = "AUT",
        ["Belgium"] = "BEL",
        ["Bosnia and Herzegovina"] = "BIH",
        ["Brazil"] = "BRA",
        ["Cabo Verde"] = "CPV",
        ["Croatia"] = "CRO",
        ["Czech Republic"] = "CZE",
        ["DR Congo"] = "COD",
        ["France"] = "FRA",
        ["Germany"] = "GER",
        ["Iraq"] = "IRQ",
        ["Korea Republic"] = "KOR",
        ["Morocco"] = "MAR",
        ["Netherlands"] = "NED",
        ["Norway"] = "NOR",
        ["Saudi Arabia"] = "KSA",
        ["Scotland"] = "SCO",
        ["South Africa"] = "RSA",
        ["Spain"] = "ESP",
        ["Sweden"] = "SWE",
        ["Switzerland"] = "SUI",
        ["Turkey"] = "TUR",
        ["Uzbekistan"] = "UZB",
        ["Republic of Korea"] = "KOR",
        ["Côte d'Ivoire"] = "CIV",
        ["Ivory Coast"] = "CIV",
        ["United States"] = "USA",
        ["IR Iran"] = "IRN",
    };

    private readonly ILogger<TeamCodeMapper> _logger;
    private readonly Dictionary<string, string> _codesByTeamName;

    public TeamCodeMapper(IWebHostEnvironment environment, ILogger<TeamCodeMapper> logger)
    {
        _logger = logger;
        var teamsJsonPath = ResolveTeamsJsonPath(environment);
        _codesByTeamName = LoadCodesByTeamName(teamsJsonPath);
    }

    public string? GetCode(string? teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            _logger.LogWarning("Cannot map an empty team name to a team code.");
            return null;
        }

        if (ManualOverrides.TryGetValue(teamName, out var overriddenCode))
        {
            return overriddenCode;
        }

        if (_codesByTeamName.TryGetValue(teamName, out var code))
        {
            return code;
        }

        _logger.LogWarning("Unknown team name '{TeamName}' could not be mapped to a team code.", teamName);
        return null;
    }

    public bool IsValidCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return _codesByTeamName.Values.Contains(code, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> LoadCodesByTeamName(string path)
    {
        var json = File.ReadAllText(path);
        var teamsByCode = JsonSerializer.Deserialize<Dictionary<string, TeamDefinition>>(json)
            ?? throw new InvalidOperationException("Failed to parse teams.json");

        return teamsByCode.Values
            .Where(team => !string.IsNullOrWhiteSpace(team.Name) && !string.IsNullOrWhiteSpace(team.Code))
            .ToDictionary(team => team.Name!, team => team.Code!, StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveTeamsJsonPath(IWebHostEnvironment environment)
    {
        var configuredPath = Environment.GetEnvironmentVariable("TEAMS_JSON_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var contentRootPath = Path.Combine(environment.ContentRootPath, "data", "teams.json");
        if (File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        return Path.Combine(environment.ContentRootPath, "..", "..", "src", "data", "teams.json");
    }

    private sealed class TeamDefinition
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("flag")]
        public string? Flag { get; init; }
    }
}
