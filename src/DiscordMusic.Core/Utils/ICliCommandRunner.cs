namespace DiscordMusic.Core.Utils;

internal interface ICliCommandRunner
{
    Task<CliCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken
    );
}
