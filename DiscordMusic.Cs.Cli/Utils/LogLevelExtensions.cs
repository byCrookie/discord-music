using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace DiscordMusic.Cs.Cli.Utils;

internal static class LogLevelExtensions
{
    public static LogEventLevel MicrosoftToSerilogLevel(this LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.None:
            case LogLevel.Critical:
                return LogEventLevel.Fatal;
            case LogLevel.Error:
                return LogEventLevel.Error;
            case LogLevel.Warning:
                return LogEventLevel.Warning;
            case LogLevel.Information:
                return LogEventLevel.Information;
            case LogLevel.Debug:
                return LogEventLevel.Debug;
            case LogLevel.Trace:
            default:
                return LogEventLevel.Verbose;
        }
    }
}