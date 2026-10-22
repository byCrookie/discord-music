using DiscordMusic.Core.Utils;

namespace DiscordMusic.Core.Tests.Utils;

public class ResultTests
{
    [Test]
    public async Task SuccessStoresValueWithoutError()
    {
        Result<int> result = 42;

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.IsInitialized).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
        await Assert.That(result.TryGetValue(out var value)).IsTrue();
        await Assert.That(value).IsEqualTo(42);
        await Assert.That(result.TryGetError(out var error)).IsFalse();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task FailureStoresSemanticErrorWithoutValue()
    {
        var expectedFailure = new ResultFailure.Custom("test.failure", "failed");
        Result<int> result = expectedFailure;

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error).IsEqualTo(expectedFailure);
        await Assert.That(result.TryGetError(out var error)).IsTrue();
        await Assert.That(error).IsEqualTo(expectedFailure);
        await Assert.That(result.TryGetValue(out _)).IsFalse();
    }

    [Test]
    public async Task TryReturnsSuccessForFunctionValue()
    {
        var result = Result<int>.Try(() => 42);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task TryConvertsUnknownExceptionToSystemFailure()
    {
        var expectedError = new InvalidOperationException("failed");

        var result = Result<int>.Try(() => throw expectedError);

        await Assert.That(result.Error).IsEqualTo(new ResultFailure.SystemFailure(expectedError));
    }

    [Test]
    public async Task TryAsyncReturnsSuccessForFunctionValue()
    {
        var result = await Result<int>.TryAsync(() => Task.FromResult(42));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task TryAsyncConvertsUnknownExceptionToSystemFailure()
    {
        var expectedError = new InvalidOperationException("failed");

        var result = await Result<int>.TryAsync(() => Task.FromException<int>(expectedError));

        await Assert.That(result.Error).IsEqualTo(new ResultFailure.SystemFailure(expectedError));
    }

    [Test]
    public async Task TryMapsCommonExceptionsToSemanticFailures()
    {
        var cancellation = new OperationCanceledException("canceled");
        var invalidArgument = new ArgumentException("invalid");
        var missingFile = new FileNotFoundException("missing");
        var missingDirectory = new DirectoryNotFoundException("missing directory");
        var ioError = new IOException("io error");
        var accessDenied = new UnauthorizedAccessException("access denied");

        await AssertFailure(
            Result<int>.Try(() => throw cancellation),
            new ResultFailure.SystemFailure(cancellation)
        );
        var invalidResult = Result<int>.Try(() => throw invalidArgument);
        var validation = invalidResult.Error.Value as ResultFailure.Validation;
        await Assert.That(validation).IsNotNull();
        await Assert.That(validation!.Fields["argument"][0]).IsEqualTo("invalid");
        await AssertFailure(
            Result<int>.Try(() => throw missingFile),
            new ResultFailure.NotFound("file")
        );
        await AssertFailure(
            Result<int>.Try(() => throw missingDirectory),
            new ResultFailure.NotFound("directory")
        );
        await AssertFailure(
            Result<int>.Try(() => throw ioError),
            new ResultFailure.SystemFailure(ioError)
        );
        await AssertFailure(
            Result<int>.Try(() => throw accessDenied),
            new ResultFailure.Forbidden(accessDenied.Message)
        );
    }

    [Test]
    public async Task TryAsyncMapsCancellationToSemanticFailure()
    {
        var cancellation = new OperationCanceledException("canceled");

        var result = await Result<int>.TryAsync(() => Task.FromException<int>(cancellation));

        await AssertFailure(result, new ResultFailure.SystemFailure(cancellation));
    }

    [Test]
    public async Task MapTransformsSuccessAndPreservesFailure()
    {
        Result<int> success = 21;
        Result<int> failed = new ResultFailure.Custom("failure", "nope");

        var mappedSuccess = success.Map(value => value * 2);
        var mappedFailure = failed.Map(value => value * 2);

        await Assert.That(mappedSuccess.Value).IsEqualTo(42);
        await Assert.That(mappedFailure.Error).IsEqualTo(failed.Error);
    }

    [Test]
    public async Task BindChainsSuccessAndSkipsFailure()
    {
        Result<int> success = 20;
        Result<int> failed = new ResultFailure.Custom("failure", "nope");
        var wasCalled = false;

        var boundSuccess = success.Bind<int>(value => value + 22);
        var boundFailure = failed.Bind<int>(value =>
        {
            wasCalled = true;
            return value;
        });

        await Assert.That(boundSuccess.Value).IsEqualTo(42);
        await Assert.That(boundFailure.Error).IsEqualTo(failed.Error);
        await Assert.That(wasCalled).IsFalse();
    }

    [Test]
    public async Task MatchSelectsTheFailureHandler()
    {
        Result<int> result = new ResultFailure.Custom("failure", "nope");

        var description = result.Match(
            value => $"value:{value}",
            failure =>
                failure switch
                {
                    ResultFailure.Custom custom => $"{custom.Code}:{custom.Message}",
                    _ => "other",
                }
        );

        await Assert.That(description).IsEqualTo("failure:nope");
    }

    [Test]
    public async Task TapRunsOnlyForSuccess()
    {
        var observed = 0;
        Result<int> success = 42;
        Result<int> failed = new ResultFailure.Custom("failure", "nope");

        success.Tap(value => observed = value);
        failed.Tap(value => observed = value);

        await Assert.That(observed).IsEqualTo(42);
    }

    [Test]
    public async Task RecoverHandlesOnlyMatchingFailureCase()
    {
        Result<int> matchingFailure = new ResultFailure.NotFound("track");
        Result<int> otherFailure = new ResultFailure.SystemFailure(new IOException());

        var recovered = matchingFailure.Recover<ResultFailure.NotFound>(_ => 42);
        var unchanged = otherFailure.Recover<ResultFailure.NotFound>(_ => 42);

        await Assert.That(recovered.Value).IsEqualTo(42);
        await Assert.That(unchanged.Error).IsEqualTo(otherFailure.Error);
    }

    [Test]
    public async Task FailureUnionSwitchCoversAllCases()
    {
        ResultFailure failure = new ResultFailure.Custom("custom", "message");

        var description = failure switch
        {
            ResultFailure.NotFound error => $"not found: {error.Resource}",
            ResultFailure.Conflict error => $"conflict: {error.Reason}",
            ResultFailure.Unauthorized => "unauthorized",
            ResultFailure.Forbidden error => $"forbidden: {error.Reason}",
            ResultFailure.Validation error => $"validation: {error.Fields.Count}",
            ResultFailure.SystemFailure error => $"system: {error.Ex.Message}",
            ResultFailure.Custom error => $"custom: {error.Code}",
        };

        await Assert.That(description).IsEqualTo("custom: custom");
    }

    [Test]
    public async Task UnwrapReturnsValueOrThrowsWithFailure()
    {
        Result<int> success = 42;
        Result<int> failure = new ResultFailure.NotFound("track");

        await Assert.That(success.Unwrap()).IsEqualTo(42);
        await Assert.ThrowsAsync<ResultUnwrapException>(() =>
        {
            failure.Unwrap();
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task UnwrapOrReturnsFallbackForFailure()
    {
        Result<int> failure = new ResultFailure.NotFound("track");

        await Assert.That(failure.UnwrapOr(10)).IsEqualTo(10);
        await Assert.That(failure.UnwrapOrDefault(20)).IsEqualTo(20);
    }

    [Test]
    public async Task UnwrapAsyncReturnsValueAndFallback()
    {
        var successTask = Task.FromResult<Result<int>>(42);
        var failureTask = Task.FromResult<Result<int>>(new ResultFailure.NotFound("track"));

        await Assert.That(await successTask.UnwrapAsync()).IsEqualTo(42);
        await Assert.That(await failureTask.UnwrapOrDefaultAsync(10)).IsEqualTo(10);
    }

    [Test]
    public async Task EnsureConvertsRejectedSuccessToFailure()
    {
        Result<int> result = 0;

        var ensured = result.Ensure(
            value => value > 0,
            _ => new ResultFailure.Custom("invalid", "must be positive")
        );

        await Assert
            .That(ensured.Error)
            .IsEqualTo(new ResultFailure.Custom("invalid", "must be positive"));
    }

    [Test]
    public async Task BindAsyncChainsSuccess()
    {
        Result<int> result = 20;

        var bound = await result.BindAsync(value => ValueTask.FromResult<Result<int>>(value + 22));

        await Assert.That(bound.Value).IsEqualTo(42);
    }

    [Test]
    public async Task DefaultResultIsUninitialized()
    {
        var result = default(Result<int>);

        await Assert.That(result.IsInitialized).IsFalse();
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.IsError).IsFalse();
    }

    private static async Task AssertFailure(Result<int> result, ResultFailure expectedFailure) =>
        await Assert.That(result.Error).IsEqualTo(expectedFailure);
}
