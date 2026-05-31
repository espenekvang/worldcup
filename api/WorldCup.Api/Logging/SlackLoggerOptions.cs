using Microsoft.Extensions.Logging;

namespace WorldCup.Api.Logging;

public class SlackLoggerOptions
{
    public string WebhookUrl { get; set; } = string.Empty;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;
}
