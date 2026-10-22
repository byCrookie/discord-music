using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DiscordMusic.Core.Utils;

/// <summary>A closed set of semantic failures that can be carried by a <see cref="Result{T}"/>.</summary>
[Union]
public readonly struct ResultFailure : IUnion
{
    public ResultFailure(NotFound value) => Value = value;

    public ResultFailure(Conflict value) => Value = value;

    public ResultFailure(Unauthorized value) => Value = value;

    public ResultFailure(Forbidden value) => Value = value;

    public ResultFailure(Validation value) => Value = value;

    public ResultFailure(SystemFailure value) => Value = value;

    public ResultFailure(Custom value) => Value = value;

    /// <summary>Gets the active failure case, or <see langword="null"/> for the default value.</summary>
    public object? Value { get; }

    /// <summary>Gets whether this value contains a failure case.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether this is the default, uninitialized value.</summary>
    public bool IsDefault => Value is null;

    public bool TryGetValue([NotNullWhen(true)] out NotFound? value)
    {
        value = Value as NotFound;
        return value is not null;
    }

    public bool TryGetValue([NotNullWhen(true)] out Conflict? value)
    {
        value = Value as Conflict;
        return value is not null;
    }

    public bool TryGetValue([NotNullWhen(true)] out Unauthorized? value)
    {
        value = Value as Unauthorized;
        return value is not null;
    }

    public bool TryGetValue([NotNullWhen(true)] out Forbidden? value)
    {
        value = Value as Forbidden;
        return value is not null;
    }

    public bool TryGetValue([NotNullWhen(true)] out Validation? value)
    {
        value = Value as Validation;
        return value is not null;
    }

    public bool TryGetValue([NotNullWhen(true)] out SystemFailure? value)
    {
        value = Value as SystemFailure;
        return value is not null;
    }

    public bool TryGetValue([NotNullWhen(true)] out Custom? value)
    {
        value = Value as Custom;
        return value is not null;
    }

    /// <summary>Converts common exceptions to the corresponding semantic failure case.</summary>
    public static ResultFailure FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            FileNotFoundException fileNotFound => new NotFound(fileNotFound.FileName ?? "file"),
            DirectoryNotFoundException => new NotFound("directory"),
            ArgumentException argumentError => CreateValidation(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [argumentError.ParamName ?? "argument"] = [argumentError.Message],
                }
            ),
            UnauthorizedAccessException accessError => new Forbidden(accessError.Message),
            _ => new SystemFailure(exception),
        };
    }

    /// <summary>The requested resource could not be found.</summary>
    public sealed record NotFound(string Resource);

    /// <summary>The operation conflicts with the existing state.</summary>
    public sealed record Conflict(string Reason);

    /// <summary>The caller is not authenticated.</summary>
    public sealed record Unauthorized;

    /// <summary>The caller is authenticated but is not allowed to perform the operation.</summary>
    public sealed record Forbidden(string Reason);

    /// <summary>One or more input fields failed validation.</summary>
    public sealed record Validation(IReadOnlyDictionary<string, string[]> Fields);

    /// <summary>An unexpected system-level failure occurred.</summary>
    public sealed record SystemFailure(Exception Ex)
    {
        public SystemFailure(string message, Exception? innerException = null)
            : this(innerException ?? new InvalidOperationException(message)) { }
    }

    /// <summary>An application-specific failure.</summary>
    public sealed record Custom(
        string Code,
        string Message,
        int StatusCode = 422,
        IReadOnlyDictionary<string, object>? Extensions = null
    );

    /// <summary>Creates a validation failure from a dictionary of field messages.</summary>
    public static Validation CreateValidation(IDictionary<string, string[]> fields) =>
        new(
            new ReadOnlyDictionary<string, string[]>(
                new Dictionary<string, string[]>(fields, StringComparer.Ordinal)
            )
        );

    /// <summary>Creates a validation failure from field/message pairs.</summary>
    public static Validation CreateValidation(
        IEnumerable<(string Field, string[] Messages)> pairs
    ) =>
        new(
            new ReadOnlyDictionary<string, string[]>(
                pairs.ToDictionary(
                    pair => pair.Field,
                    pair => pair.Messages,
                    StringComparer.Ordinal
                )
            )
        );
}
