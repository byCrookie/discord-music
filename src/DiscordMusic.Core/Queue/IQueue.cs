using System.Diagnostics.CodeAnalysis;

namespace DiscordMusic.Core.Queue;

public interface IQueue<T>
    where T : class
{
    void EnqueueLast(T item);
    void EnqueueFirst(T item);
    bool TryDequeue([MaybeNullWhen(false)] out T item);
    bool TryPeek([MaybeNullWhen(false)] out T item);
    ICollection<T> Items();
    void Clear();
    void SkipTo(int index);
    int Count();
    void Shuffle();
}
