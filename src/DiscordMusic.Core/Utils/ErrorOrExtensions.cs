using ErrorOr;

namespace DiscordMusic.Core.Utils;

public static class ErrorOrExtensions
{
    public static string ToErrorContent<T>(this ErrorOr<T> value)
    {
        if (!value.IsError)
        {
            throw new InvalidOperationException("Value is not an error");
        }

        return value.Errors.Count == 1
            ? value.FirstError.ToErrorContent()
            : string.Join(Environment.NewLine, value.Errors.Select(e => e.ToErrorContent()));
    }

    private static string ToErrorContent(this Error error)
    {
        return $"""
            ### **ERROR**: {error.Code}
            ```{error.Description}```
            """;
    }
}
