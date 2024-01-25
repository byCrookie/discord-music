using DiscordMusic.Cli.Utils;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DiscordMusic.Cli.Logging;

internal static class CliLogger
{
    public static LoggerConfiguration Create(string[] args)
    {
        var verbosity = args.GetArgValue("--verbosity", LogLevel.Information);
        var logEventLevel = verbosity.MicrosoftToSerilogLevel();
        
        var configuration = new LoggerConfiguration();

        var logFile = args.GetArgValue<FileInfo?>("--log-file");
        if (logFile is not null)
        {
            configuration.WriteTo.File(
                logFile.FullName,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 100_000_000,
                retainedFileCountLimit: 10,
                rollingInterval: RollingInterval.Infinite
            ).MinimumLevel.Is(logEventLevel);
        }
        
        var quiet = args.GetArgValue("--quiet", true);
        if (!quiet)
        {
            configuration.WriteTo.Console()
                .MinimumLevel.Is(logEventLevel);
        }
        
        return configuration;
    }
}