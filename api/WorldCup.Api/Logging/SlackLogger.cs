using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WorldCup.Api.Logging;

public class SlackLogger : ILogger
{
    private readonly string _categoryName;
    private readonly SlackLoggerOptions _options;
    private readonly HttpClient _httpClient;

    public SlackLogger(string categoryName, SlackLoggerOptions options, HttpClient httpClient)
    {
        _categoryName = categoryName;
        _options = options;
        _httpClient = httpClient;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var emoji = logLevel switch
        {
            LogLevel.Warning => ":warning:",
            LogLevel.Error => ":x:",
            LogLevel.Critical => ":fire:",
            _ => ":information_source:"
        };

        var text = $"{emoji} *[{logLevel}]* `{_categoryName}`\n{message}";
        if (exception is not null)
        {
            text += $"\n```\n{exception}\n```";
        }

        // Fire-and-forget — don't block the request pipeline
        _ = SendAsync(text);
    }

    private async Task SendAsync(string text)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { text });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(_options.WebhookUrl, content);
        }
        catch
        {
            // Swallow errors — we don't want logging failures to crash the app
        }
    }
}
