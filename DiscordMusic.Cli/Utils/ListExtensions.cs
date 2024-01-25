namespace DiscordMusic.Cli.Utils;

internal static class ListExtensions
{
    public static void AddAll<T>(this ICollection<T> source, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            source.Add(item);
        }
    }
    
    public static async IAsyncEnumerable<TResult> SelectAsync<TItem, TResult>(this IEnumerable<TItem> source, Func<TItem, Task<TResult>> selector)
    {
        foreach (var item in source)
        {
            yield return await selector(item);
        }
    }
}