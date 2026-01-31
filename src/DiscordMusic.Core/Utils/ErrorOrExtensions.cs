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
            : $"""
                ### Something went wrong
                {string.Join(Environment.NewLine, value.Errors.Select(e => e.ToErrorListItem()))}
                """;
    }

    extension(Error error)
    {
        private string ToErrorContent()
        {
            var detail = FormatDetail(error.Description);

            return $"""
                ### Something went wrong
                {FirstLineOrFallback(error.Description, "Please try again.")}
                -# Code: `{error.Code}`
                {detail}
                """;
        }

        private string ToErrorListItem()
        {
            var summary = FirstLineOrFallback(error.Description, "Unknown error.");
            var detail = FormatDetail(error.Description);

            var detailBlock = string.IsNullOrWhiteSpace(detail)
                ? string.Empty
                : $"{Environment.NewLine}  {detail}";

            return $"- {summary}{Environment.NewLine}  -# Code: `{error.Code}`{detailBlock}";
        }
    }

    private static string FirstLineOrFallback(string? text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var idx = text.IndexOfAny(['\r', '\n']);
        return idx >= 0 ? text[..idx].Trim() : text.Trim();
    }

    private static string FormatDetail(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var trimmed = description.Trim();

        if (!trimmed.Contains('\n') && !trimmed.Contains('\r'))
        {
            return string.Empty;
        }

        return $"""
            -# Details:
            ```
            {trimmed}
            ```
            """.TrimEnd();
    }
}
