using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WorldCup.Api.Services;

namespace WorldCup.Api.Tests;

public class TeamCodeMapperTests
{
    private static TeamCodeMapper CreateMapper(string teamsJsonPath)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(Path.GetDirectoryName(teamsJsonPath)!);

        var logger = Substitute.For<ILogger<TeamCodeMapper>>();

        Environment.SetEnvironmentVariable("TEAMS_JSON_PATH", teamsJsonPath);
        try
        {
            return new TeamCodeMapper(env, logger);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEAMS_JSON_PATH", null);
        }
    }

    private static string GetTeamsJsonPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "data", "teams.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        var json = """
            {
              "BRA": { "code": "BRA", "name": "Brasil", "flag": "🇧🇷" },
              "GER": { "code": "GER", "name": "Tyskland", "flag": "🇩🇪" },
              "ESP": { "code": "ESP", "name": "Spania", "flag": "🇪🇸" },
              "FRA": { "code": "FRA", "name": "Frankrike", "flag": "🇫🇷" }
            }
            """;
        var tmpPath = Path.GetTempFileName();
        File.WriteAllText(tmpPath, json);
        return tmpPath;
    }

    [Fact]
    public void GetCode_KnownTeamName_ReturnsCorrectCode()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var code = mapper.GetCode("Brasil");

        code.Should().Be("BRA");
    }

    [Fact]
    public void GetCode_ManualOverride_KoreaRepublic_ReturnsKOR()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var code = mapper.GetCode("Korea Republic");

        code.Should().Be("KOR");
    }

    [Fact]
    public void GetCode_ManualOverride_UnitedStates_ReturnsUSA()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var code = mapper.GetCode("United States");

        code.Should().Be("USA");
    }

    [Fact]
    public void GetCode_UnknownTeamName_ReturnsNull()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var code = mapper.GetCode("Narnia FC");

        code.Should().BeNull();
    }

    [Fact]
    public void GetCode_NullTeamName_ReturnsNull()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var code = mapper.GetCode(null);

        code.Should().BeNull();
    }

    [Fact]
    public void GetCode_EmptyTeamName_ReturnsNull()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var code = mapper.GetCode(string.Empty);

        code.Should().BeNull();
    }

    [Fact]
    public void IsValidCode_KnownCode_ReturnsTrue()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var valid = mapper.IsValidCode("BRA");

        valid.Should().BeTrue();
    }

    [Fact]
    public void IsValidCode_UnknownCode_ReturnsFalse()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var valid = mapper.IsValidCode("XYZ");

        valid.Should().BeFalse();
    }

    [Fact]
    public void IsValidCode_NullCode_ReturnsFalse()
    {
        var mapper = CreateMapper(GetTeamsJsonPath());

        var valid = mapper.IsValidCode(null);

        valid.Should().BeFalse();
    }
}
