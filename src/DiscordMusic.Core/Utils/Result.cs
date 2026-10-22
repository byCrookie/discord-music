namespace DiscordMusic.Core.Utils;

/// <summary>A success value or a semantic failure, stored without boxing value-type results.</summary>
public readonly struct Result<T>
{
    private enum State : byte
    {
        Uninitialized,
        Success,
        Failure,
    }

    private readonly T? _value;
    private readonly ResultFailure _error;
    private readonly State _state;

    private Result(T? value, ResultFailure error, State state) =>
        (_value, _error, _state) = (value, error, state);

    public bool IsSuccess => _state == State.Success;
    public bool IsError => _state == State.Failure;
    public bool IsInitialized => _state != State.Uninitialized;

    /// <summary>Gets the success value, or throws if this is not a success.</summary>
    public T? Value =>
        IsSuccess ? _value : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>Gets the failure, or throws if this is not a failure.</summary>
    public ResultFailure Error =>
        IsError ? _error : throw new InvalidOperationException("A successful result has no error.");

    public static Result<T> Success(T? value) => new(value, default, State.Success);

    public static Result<T> Failure(ResultFailure error)
    {
        return !error.HasValue
            ? throw new ArgumentException("A failure case must be initialized.", nameof(error))
            : new Result<T>(default, error, State.Failure);
    }

    public static Result<T> Failure(Exception error) => Failure(ResultFailure.FromException(error));

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(ResultFailure failure) => Failure(failure);

    public static implicit operator Result<T>(Exception error) => Failure(error);

    public static implicit operator Result<T>(ResultFailure.NotFound failure) =>
        Failure(new ResultFailure(failure));

    public static implicit operator Result<T>(ResultFailure.Conflict failure) =>
        Failure(new ResultFailure(failure));

    public static implicit operator Result<T>(ResultFailure.Unauthorized failure) =>
        Failure(new ResultFailure(failure));

    public static implicit operator Result<T>(ResultFailure.Forbidden failure) =>
        Failure(new ResultFailure(failure));

    public static implicit operator Result<T>(ResultFailure.Validation failure) =>
        Failure(new ResultFailure(failure));

    public static implicit operator Result<T>(ResultFailure.SystemFailure failure) =>
        Failure(new ResultFailure(failure));

    public static implicit operator Result<T>(ResultFailure.Custom failure) =>
        Failure(new ResultFailure(failure));

    /// <summary>Returns the success value, or throws when this is a failure or default result.</summary>
    public T? Unwrap() =>
        IsSuccess
            ? _value
            : throw new ResultUnwrapException(IsError ? _error : (ResultFailure?)null);

    /// <summary>Returns the success value or the supplied fallback.</summary>
    public T? UnwrapOr(T? fallback) => IsSuccess ? _value : fallback;

    /// <summary>Returns the success value or the supplied default value.</summary>
    public T? UnwrapOrDefault(T? defaultValue) => UnwrapOr(defaultValue);

    /// <summary>Invokes a function and captures any exception as a semantic failure.</summary>
    public static Result<T> Try(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        try
        {
            return Success(function());
        }
        catch (Exception error)
        {
            return Failure(error);
        }
    }

    /// <summary>Awaits a function and captures any exception as a semantic failure.</summary>
    public static async Task<Result<T>> TryAsync(Func<Task<T>> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        try
        {
            return Success(await function().ConfigureAwait(false));
        }
        catch (Exception error)
        {
            return Failure(error);
        }
    }

    /// <summary>Routes the value or failure to its matching handler.</summary>
    public TResult Match<TResult>(
        Func<T?, TResult> onSuccess,
        Func<ResultFailure, TResult> onFailure
    )
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (IsSuccess)
        {
            return onSuccess(_value);
        }

        return IsError
            ? onFailure(_error)
            : throw new InvalidOperationException("Cannot match an uninitialized result.");
    }

    /// <summary>Runs a side effect for the success or failure case.</summary>
    public void Switch(Action<T?> onSuccess, Action<ResultFailure> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (IsSuccess)
        {
            onSuccess(_value);
            return;
        }

        if (!IsError)
        {
            throw new ResultUnwrapException(null);
        }

        onFailure(_error);
    }

    /// <summary>Transforms a success value while preserving failures.</summary>
    public Result<TOut> Map<TOut>(Func<T?, TOut?> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (IsError)
        {
            return Result<TOut>.Failure(_error);
        }

        return IsSuccess
            ? Result<TOut>.Success(mapper(_value))
            : Result<TOut>.Failure(UninitializedFailure());
    }

    /// <summary>Chains a result-producing operation, skipping it after a failure.</summary>
    public Result<TOut> Bind<TOut>(Func<T?, Result<TOut>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (IsError)
        {
            return Result<TOut>.Failure(_error);
        }

        return IsSuccess ? binder(_value) : Result<TOut>.Failure(UninitializedFailure());
    }

    /// <summary>Runs a side effect on a success value and returns this result unchanged.</summary>
    public Result<T> Tap(Action<T?> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            action(_value);
        }

        return this;
    }

    /// <summary>Replaces a matching failure with a success value.</summary>
    public Result<T> Recover<TFailure>(Func<TFailure, T?> recovery)
        where TFailure : class
    {
        ArgumentNullException.ThrowIfNull(recovery);

        return _error.Value is TFailure failure ? Success(recovery(failure)) : this;
    }

    /// <summary>Returns a failure when a successful value does not satisfy a predicate.</summary>
    public Result<T> Ensure(Func<T?, bool> predicate, Func<T?, ResultFailure> failureFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(failureFactory);

        if (!IsSuccess || predicate(_value))
        {
            return this;
        }

        return Failure(failureFactory(_value));
    }

    /// <summary>Asynchronously transforms a success value while preserving failures.</summary>
    public async ValueTask<Result<TOut>> MapAsync<TOut>(Func<T?, ValueTask<TOut?>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (IsError)
        {
            return Result<TOut>.Failure(_error);
        }

        return IsSuccess
            ? Result<TOut>.Success(await mapper(_value).ConfigureAwait(false))
            : Result<TOut>.Failure(UninitializedFailure());
    }

    /// <summary>Asynchronously chains a result-producing operation, skipping it after a failure.</summary>
    public async ValueTask<Result<TOut>> BindAsync<TOut>(Func<T?, ValueTask<Result<TOut>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (IsError)
        {
            return Result<TOut>.Failure(_error);
        }

        return IsSuccess
            ? await binder(_value).ConfigureAwait(false)
            : Result<TOut>.Failure(UninitializedFailure());
    }

    /// <summary>Asynchronously runs a side effect on a success value and returns this result.</summary>
    public async ValueTask<Result<T>> TapAsync(Func<T?, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            await action(_value).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Asynchronously replaces a matching failure with a success value.</summary>
    public async ValueTask<Result<T>> RecoverAsync<TFailure>(Func<TFailure, ValueTask<T?>> recovery)
        where TFailure : class
    {
        ArgumentNullException.ThrowIfNull(recovery);

        return _error.Value is TFailure failure
            ? Success(await recovery(failure).ConfigureAwait(false))
            : this;
    }

    public bool TryGetValue(out T? value)
    {
        value = _value;
        return IsSuccess;
    }

    public bool TryGetError(out ResultFailure? error)
    {
        if (IsError)
        {
            error = _error;
            return true;
        }

        error = null;
        return false;
    }

    private static ResultFailure.SystemFailure UninitializedFailure() =>
        new(new InvalidOperationException("Cannot compose an uninitialized result."));
}
