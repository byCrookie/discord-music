using Flurl.Util;

namespace DiscordMusic.Core.Utils;

internal static class CollectionExtensions
{
    public static T GetRequired<T>(this INameValueListBase<T> query, string key) where T : class
    {
        if (query.TryGetFirst(key, out var value))
        {
            return value;
        }
        
        throw new InvalidOperationException($"Query parameter '{key}' not found.");
    }

    public static T? Get<T>(this INameValueListBase<T> query, string key) where T : class
    {
        return query.TryGetFirst(key, out var value) ? value : null;
    }
}