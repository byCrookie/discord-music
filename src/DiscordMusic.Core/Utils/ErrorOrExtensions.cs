using ErrorOr;

namespace DiscordMusic.Core.Utils;

public static class ErrorOrExtensions
{
    public static string ToPrint<T>(this ErrorOr<T> value)
    {
        if (!value.IsError)
        {
            throw new InvalidOperationException("Value is not an error");
        }

        return value.Errors.Count == 1
            ? value.FirstError.ToPrint()
            : string.Join(Environment.NewLine, value.Errors.Select(e => e.ToPrint()));
    }

    private static string ToPrint(this Error error)
    {
        return $"[{error.Code}] {error.Description}";
    }
}
