using ErrorOr;

namespace DiscordMusic.Core.Utils;

public static class ErrorOrExtensions
{
    public static string ToContent<T>(this ErrorOr<T> value)
    {
        if (!value.IsError)
        {
            throw new InvalidOperationException("Value is not an error");
        }

        return value.Errors.Count == 1
            ? value.FirstError.ToContent()
            : string.Join(Environment.NewLine, value.Errors.Select(e => e.ToContent()));
    }

    private static string ToContent(this Error error)
    {
        return $"""
            ### **ERROR**: {error.Code}
            ```{error.Description}```
            """;
    }
}
