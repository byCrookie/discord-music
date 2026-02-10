using System.Collections.Concurrent;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class VoiceCommandBuffer
{
    private sealed class Entry
    {
        public MemoryStream Stream { get; } = new();
        public DateTimeOffset LastAppendUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private readonly ConcurrentDictionary<uint, Entry> _buffers = new();

    public void Append(uint ssrc, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        var entry = _buffers.GetOrAdd(ssrc, _ => new Entry());
        lock (entry)
        {
            entry.Stream.Write(bytes);
            entry.LastAppendUtc = DateTimeOffset.UtcNow;
        }
    }

    public int PeekLength(uint ssrc)
    {
        if (!_buffers.TryGetValue(ssrc, out var entry))
        {
            return 0;
        }

        lock (entry)
        {
            return (int)entry.Stream.Length;
        }
    }

    public DateTimeOffset? GetLastAppendUtc(uint ssrc)
    {
        if (!_buffers.TryGetValue(ssrc, out var entry))
        {
            return null;
        }

        lock (entry)
        {
            return entry.LastAppendUtc;
        }
    }

    public byte[] SnapshotAndClear(uint ssrc)
    {
        if (!_buffers.TryGetValue(ssrc, out var entry))
        {
            return [];
        }

        lock (entry)
        {
            var data = entry.Stream.ToArray();
            entry.Stream.SetLength(0);

            // If the stream is now empty, attempt to remove it to avoid holding onto many idle entries.
            // Best effort: if another thread appends concurrently, TryRemove will fail and that's fine.
            if (entry.Stream.Length == 0)
            {
                _buffers.TryRemove(new KeyValuePair<uint, Entry>(ssrc, entry));
            }

            return data;
        }
    }

    public uint[] GetSsrcs() => _buffers.Keys.ToArray();
}
