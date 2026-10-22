namespace DiscordMusic.Core.Utils;

/// <summary>Thrown when <see cref="Result{T}.Unwrap"/> is called on a failure or default result.</summary>
public sealed class ResultUnwrapException(ResultFailure? failure)
    : InvalidOperationException(
        failure is { } error
            ? $"Cannot unwrap failed result: {error.Value}"
            : "Cannot unwrap an uninitialized result."
    )
{
    public ResultFailure? Failure { get; } = failure;
}
