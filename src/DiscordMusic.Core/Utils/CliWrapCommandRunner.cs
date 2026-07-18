using System.Text;
using CliWrap;

namespace DiscordMusic.Core.Utils;

internal sealed class CliWrapCommandRunner : ICliCommandRunner
{
    public async Task<CliCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken
    )
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var command = Cli.Wrap(fileName)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr));

        if (environment is not null)
        {
            command = command.WithEnvironmentVariables(environment);
        }

        var result = await command.ExecuteAsync(cancellationToken);
        return new CliCommandResult(result.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
