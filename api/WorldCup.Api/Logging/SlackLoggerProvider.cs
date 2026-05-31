using Microsoft.Extensions.Logging;

namespace WorldCup.Api.Logging;

[ProviderAlias("Slack")]
public class SlackLoggerProvider : ILoggerProvider
{
    private readonly SlackLoggerOptions _options;
    private readonly HttpClient _httpClient;

    public SlackLoggerProvider(SlackLoggerOptions options)
    {
        _options = options;
        _httpClient = new HttpClient();
    }

    public ILogger CreateLogger(string categoryName) =>
        new SlackLogger(categoryName, _options, _httpClient);

    public void Dispose() => _httpClient.Dispose();
}
