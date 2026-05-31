using Microsoft.Extensions.Logging;

namespace WorldCup.Api.Logging;

public static class SlackLoggerExtensions
{
    public static ILoggingBuilder AddSlack(this ILoggingBuilder builder, Action<SlackLoggerOptions> configure)
    {
        var options = new SlackLoggerOptions();
        configure(options);
        builder.AddProvider(new SlackLoggerProvider(options));
        return builder;
    }
}
