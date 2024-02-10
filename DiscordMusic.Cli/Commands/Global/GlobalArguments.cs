using Cocona;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Commands.Global;

[UsedImplicitly]
public record GlobalArguments(
    [Option("verbosity")] LogLevel Verbosity = GlobalArguments.DefaultVerbosity,
    [Option("quiet")] bool Quiet = GlobalArguments.DefaultQuiet,
    [Option("log-file")] string? LogFile = GlobalArguments.DefaultLogFile
) : ICommandParameterSet
{
    public const LogLevel DefaultVerbosity = LogLevel.Information;
    public const bool DefaultQuiet = false;
    public const string? DefaultLogFile = null;
}