using DiscordMusic.Cli.Commands.Global;
using DiscordMusic.Cli.Utils;
using Serilog;

namespace DiscordMusic.Cli.Logging;

internal static class Logging
{
    public static LoggerConfiguration Initialize(string[] args)
    {
        var verbosity = args.GetArgValue("--verbosity", GlobalArguments.DefaultVerbosity);
        var logEventLevel = verbosity.MicrosoftToSerilogLevel();
        
        var configuration = new LoggerConfiguration();

        var logFile = args.GetArgValue<string?>("--log-file");
        if (logFile is not null)
        {
            configuration.WriteTo.File(
                logFile,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1*10^7,
                retainedFileCountLimit: 10,
                rollingInterval: RollingInterval.Infinite
            ).MinimumLevel.Is(logEventLevel);
        }
        
        var quiet = args.GetArgValue("--quiet", GlobalArguments.DefaultQuiet);
        if (!quiet)
        {
            configuration.WriteTo.Console()
                .MinimumLevel.Is(logEventLevel);
        }
        
        return configuration;
    }
}