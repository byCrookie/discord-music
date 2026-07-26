using System.Diagnostics;
using System.Text;
using CliWrap;
using DiscordMusic.Core.Observability;

namespace DiscordMusic.Core.Utils;

internal sealed class CliWrapCommandRunner(TimeProvider timeProvider) : ICliCommandRunner
{
    public async Task<CliCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken
    )
    {
        var startedAt = timeProvider.GetTimestamp();
        var executableName = Path.GetFileName(fileName);
        var resultTag = "completed";
        using var activity = DiscordMusicObservability.StartActivity(
            "process.execute",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetTag(activity, "process.executable.name", executableName);

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

        try
        {
            var result = await command.ExecuteAsync(cancellationToken);
            DiscordMusicObservability.SetTag(activity, "process.exit_code", result.ExitCode);

            if (result.ExitCode != 0)
            {
                resultTag = "failed";
                activity?.SetStatus(ActivityStatusCode.Error, "non_zero_exit_code");
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }

            return new CliCommandResult(result.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch (Exception ex)
        {
            resultTag = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            var tags = DiscordMusicObservability.ExternalRequestTags(
                executableName,
                "process.execute",
                resultTag
            );
            DiscordMusicObservability.ExternalRequests.Add(1, tags);
            DiscordMusicObservability.ExternalRequestDuration.Record(
                timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                tags
            );
        }
    }
}
