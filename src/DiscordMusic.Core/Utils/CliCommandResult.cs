namespace DiscordMusic.Core.Utils;

internal sealed record CliCommandResult(int ExitCode, string StandardOutput, string StandardError);
