using System.Buffers;

namespace DiscordMusic.Core.Discord.VoiceCommands;

/// <summary>
/// Super simple per-SSRC audio buffer.
/// Collects raw received frames and allows snapshot+clear.
/// </summary>
internal sealed class VoiceCommandBuffer
{
    private readonly Lock _lock = new();
    private byte[] _buffer = Array.Empty<byte>();
    private int _length;
    private DateTimeOffset _lastAppendUtc = DateTimeOffset.UtcNow;
    private bool _disposed;

    // Avoid keeping very large buffers around forever after a flush.
    private const int ReturnToPoolWhenEmptyOverBytes = 1024 * 1024;

    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            var required = checked(_length + bytes.Length);
            EnsureCapacity(required);
            bytes.CopyTo(_buffer.AsSpan(_length));
            _length = required;
            _lastAppendUtc = DateTimeOffset.UtcNow;
        }
    }

    public int PeekLength()
    {
        lock (_lock)
        {
            if (_disposed)
                return 0;

            return _length;
        }
    }

    public DateTimeOffset? GetLastAppendUtc()
    {
        lock (_lock)
        {
            if (_disposed)
                return null;

            return _length == 0 ? null : _lastAppendUtc;
        }
    }

    public byte[] SnapshotAndClear()
    {
        lock (_lock)
        {
            if (_disposed)
                return Array.Empty<byte>();

            if (_length == 0)
                return Array.Empty<byte>();

            var data = new byte[_length];
            _buffer.AsSpan(0, _length).CopyTo(data);
            _length = 0;

            if (_buffer.Length > ReturnToPoolWhenEmptyOverBytes)
            {
                ReturnBuffer();
            }

            return data;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            ReturnBuffer();
        }
    }

    private void EnsureCapacity(int required)
    {
        if (_disposed)
            return;

        if (_buffer.Length >= required)
            return;

        var newSize = Math.Max(required, Math.Max(1024, _buffer.Length * 2));
        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

        if (_length > 0)
        {
            _buffer.AsSpan(0, _length).CopyTo(newBuffer);
        }

        ReturnBuffer();
        _buffer = newBuffer;
    }

    private void ReturnBuffer()
    {
        if (_buffer.Length != 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = Array.Empty<byte>();
        }

        _length = 0;
    }
}
