using Humanizer;
using ValueOf;

namespace DiscordMusic.Core.Audio;

public class Pcm16Bytes : ValueOf<long, Pcm16Bytes>
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const int BitsPerSample = 16;
    public const int BytesPerSample = BitsPerSample / 8;

    public TimeSpan ToTime() =>
        TimeSpan.FromSeconds(1d * Value / (SampleRate * Channels * BytesPerSample));

    public static Pcm16Bytes ToBytes(TimeSpan time) =>
        From((long)Math.Ceiling(1d * time.TotalSeconds * SampleRate * Channels * BytesPerSample));

    public ByteSize Humanize()
    {
        return ByteSize.FromBytes(Value);
    }

    public static Pcm16Bytes operator +(Pcm16Bytes a, Pcm16Bytes b) => From(a.Value + b.Value);

    public static Pcm16Bytes operator -(Pcm16Bytes a, Pcm16Bytes b) => From(a.Value - b.Value);

    public static bool operator >(Pcm16Bytes a, long b) => a.Value > b;

    public static bool operator <(Pcm16Bytes a, long b) => a.Value < b;

    public static implicit operator long(Pcm16Bytes pcm16Bytes) => pcm16Bytes.Value;
}
