using ByteSizeLib;
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
        if (!string.IsNullOrWhiteSpace(logFile))
        {
            configuration.WriteTo.File(
                logFile,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: (long)ByteSize.FromMegaBytes(50).Bytes,
                retainedFileCountLimit: 2
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