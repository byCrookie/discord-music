using ErrorOr;

namespace DiscordMusic.Core.Utils;

public static class ErrorExtensions
{
    public static class MetadataKeys
    {
        public const string Operation = "operation";
        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStack = "exception.stack";
    }

    extension(Error error)
    {
        public Error WithMetadata(string key, object? value)
        {
            if (value is null)
            {
                return error;
            }

            var metadata = error.Metadata is not null
                ? new Dictionary<string, object>(error.Metadata)
                : new Dictionary<string, object>();

            metadata[key] = value;

            return Error.Custom(
                type: error.NumericType,
                code: error.Code,
                description: error.Description,
                metadata: metadata
            );
        }

        public Error WithMetadata(IReadOnlyDictionary<string, object?> metadata)
        {
            if (metadata.Count == 0)
            {
                return error;
            }

            var newMetadata = error.Metadata is not null
                ? new Dictionary<string, object>(error.Metadata)
                : new Dictionary<string, object>();

            foreach (var (key, value) in metadata)
            {
                if (value is not null)
                {
                    newMetadata[key] = value;
                }
            }

            return Error.Custom(
                type: error.NumericType,
                code: error.Code,
                description: error.Description,
                metadata: newMetadata
            );
        }

        public Error WithException(Exception exception, bool includeStackTrace = false)
        {
            var metadata = error.Metadata is not null
                ? new Dictionary<string, object>(error.Metadata)
                : new Dictionary<string, object>();

            metadata[MetadataKeys.ExceptionType] =
                exception.GetType().FullName ?? exception.GetType().Name;
            metadata[MetadataKeys.ExceptionMessage] = exception.Message;

            if (includeStackTrace && !string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                metadata[MetadataKeys.ExceptionStack] = exception.StackTrace!;
            }

            return Error.Custom(
                type: error.NumericType,
                code: error.Code,
                description: error.Description,
                metadata: metadata
            );
        }
    }

    extension<T>(ErrorOr<T> value)
    {
        public ErrorOr<T> WithMetadata(string key, object? metadataValue)
        {
            if (!value.IsError)
            {
                return value;
            }

            var updatedErrors = value
                .Errors.Select(e => e.WithMetadata(key, metadataValue))
                .ToList();
            return updatedErrors;
        }

        public ErrorOr<T> WithMetadata(IReadOnlyDictionary<string, object?> metadata)
        {
            if (!value.IsError)
            {
                return value;
            }

            var updatedErrors = value.Errors.Select(e => e.WithMetadata(metadata)).ToList();
            return updatedErrors;
        }

        public string ToErrorContent(bool includeCodeBlockDetails = true)
        {
            if (!value.IsError)
            {
                throw new InvalidOperationException("Value is not an error");
            }

            return value.Errors.Count == 1
                ? value.FirstError.ToErrorContent(includeCodeBlockDetails)
                : $"""
                   ### Something went wrong
                   {string.Join(
                       Environment.NewLine,
                       value.Errors.Select(e => e.ToErrorListItem(includeCodeBlockDetails))
                   )}
                   """;
        }
    }

    extension(Error error)
    {
        private string ToErrorContent(bool includeCodeBlockDetails)
        {
            var summary = FirstLineOrFallback(error.Description, "Please try again.");
            var detail = includeCodeBlockDetails ? FormatDetail(error) : string.Empty;
            var detailBlock = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;

            return $"""
                ### Something went wrong
                {summary}
                -# Code: `{error.Code}`
                {detailBlock}
                """.TrimEnd();
        }

        private string ToErrorListItem(bool includeCodeBlockDetails)
        {
            var summary = FirstLineOrFallback(error.Description, "Unknown error.");
            var detail = includeCodeBlockDetails ? FormatDetail(error) : string.Empty;

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

    private static string FormatDetail(Error error)
    {
        var lines = new List<string>();

        var fromDescription = FormatDetailFromDescription(error.Description);
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            lines.Add(fromDescription);
        }

        if (error.Metadata is not null && error.Metadata.Count > 0)
        {
            foreach (var key in PriorityDetailKeys)
            {
                if (error.Metadata.TryGetValue(key, out var v))
                {
                    lines.Add($"{key}: {v}");
                }
            }

            foreach (var (k, v) in error.Metadata.OrderBy(kv => kv.Key))
            {
                if (PriorityDetailKeys.Contains(k))
                {
                    continue;
                }

                lines.Add($"{k}: {v}");
            }
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var body = string.Join(Environment.NewLine, lines.Select(l => l.TrimEnd()));

        return $"""
            -# Details:
            ```
            {body}
            ```
            """.TrimEnd();
    }

    private static readonly HashSet<string> PriorityDetailKeys =
    [
        MetadataKeys.Operation,
        MetadataKeys.ExceptionType,
        MetadataKeys.ExceptionMessage,
        MetadataKeys.ExceptionStack,
    ];

    private static string FormatDetailFromDescription(string? description)
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

        return trimmed;
    }
}
