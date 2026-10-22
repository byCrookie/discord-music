namespace DiscordMusic.Core.Utils;

/// <summary>Async helpers for results wrapped in <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>.</summary>
public static class ResultAsyncExtensions
{
    /// <summary>Awaits a result and unwraps its success value.</summary>
    public static async Task<T?> UnwrapAsync<T>(this Task<Result<T>> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return (await resultTask.ConfigureAwait(false)).Unwrap();
    }

    /// <summary>Awaits a result and unwraps its success value.</summary>
    public static async ValueTask<T?> UnwrapAsync<T>(this ValueTask<Result<T>> resultTask) =>
        (await resultTask.ConfigureAwait(false)).Unwrap();

    /// <summary>Awaits a result and returns a fallback when it is not successful.</summary>
    public static async Task<T?> UnwrapOrDefaultAsync<T>(
        this Task<Result<T>> resultTask,
        T? defaultValue
    )
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        return (await resultTask.ConfigureAwait(false)).UnwrapOr(defaultValue);
    }

    /// <summary>Awaits a result and returns a fallback when it is not successful.</summary>
    public static async ValueTask<T?> UnwrapOrDefaultAsync<T>(
        this ValueTask<Result<T>> resultTask,
        T? defaultValue
    ) => (await resultTask.ConfigureAwait(false)).UnwrapOr(defaultValue);
}
