using Cocona;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Commands.Global;

public record GlobalArguments(
    [Option("verbosity")] LogLevel Verbosity = LogLevel.Information,
    [Option("quiet")] bool Quiet = true,
    [Option("log-file")] FileInfo? LogFile = null
) : ICommandParameterSet;