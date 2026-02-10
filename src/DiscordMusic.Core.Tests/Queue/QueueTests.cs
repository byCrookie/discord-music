using DiscordMusic.Core.Queue;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordMusic.Core.Tests.Queue;

public class QueueTests
{
    [Test]
    public async Task SkipTo_WhenIndexIsInvalid_DoesNotChangeQueue()
    {
        var q = new DiscordMusic.Core.Queue.Queue<string>(
            NullLogger<DiscordMusic.Core.Queue.Queue<string>>.Instance
        );
        q.EnqueueLast("a");
        q.EnqueueLast("b");

        q.SkipTo(-1);
        q.SkipTo(2);

        await Assert.That(q.Items()).IsEquivalentTo(["a", "b"]);
    }

    [Test]
    public async Task SkipTo_WhenIndexIsValid_RemovesItemsBeforeIndex()
    {
        var q = new DiscordMusic.Core.Queue.Queue<string>(
            NullLogger<DiscordMusic.Core.Queue.Queue<string>>.Instance
        );
        q.EnqueueLast("a");
        q.EnqueueLast("b");
        q.EnqueueLast("c");

        q.SkipTo(2);

        await Assert.That(q.Items()).IsEquivalentTo(["c"]);
    }

    [Test]
    public async Task TryDequeue_WhenEmpty_ReturnsFalseAndNull()
    {
        var q = new DiscordMusic.Core.Queue.Queue<string>(
            NullLogger<DiscordMusic.Core.Queue.Queue<string>>.Instance
        );

        var ok = q.TryDequeue(out var item);

        await Assert.That(ok).IsFalse();
        await Assert.That(item).IsNull();
    }

    [Test]
    public async Task Shuffle_WhenCountLessThanTwo_IsStable()
    {
        var q0 = new DiscordMusic.Core.Queue.Queue<string>(
            NullLogger<DiscordMusic.Core.Queue.Queue<string>>.Instance
        );
        q0.Shuffle();
        await Assert.That(q0.Items()).IsEmpty();

        var q1 = new DiscordMusic.Core.Queue.Queue<string>(
            NullLogger<DiscordMusic.Core.Queue.Queue<string>>.Instance
        );
        q1.EnqueueLast("a");
        q1.Shuffle();
        await Assert.That(q1.Items()).IsEquivalentTo(["a"]);
    }

    [Test]
    public async Task EnqueueFirst_WhenAddingMultipleItemsInReverse_PreservesOriginalOrder()
    {
        var q = new DiscordMusic.Core.Queue.Queue<string>(
            NullLogger<DiscordMusic.Core.Queue.Queue<string>>.Instance
        );

        // Simulate GuildSession's behavior: when prepending a list of tracks,
        // we enqueue in reverse so the first item becomes next.
        var original = new[] { "t1", "t2", "t3" };
        foreach (var item in original.Reverse())
        {
            q.EnqueueFirst(item);
        }

        await Assert.That(q.Items()).IsEquivalentTo(["t1", "t2", "t3"]);
    }
}
