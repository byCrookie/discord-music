using System.Threading.Channels;

namespace DiscordMusic.Core.Rewrite;

public class DownloadQueue
{
    private readonly Channel<AudioMetadata> _channel = Channel.CreateBounded<AudioMetadata>(100);

    public ChannelWriter<AudioMetadata> Writer => _channel.Writer;
    public ChannelReader<AudioMetadata> Reader => _channel.Reader;
}

public class SearchQueue
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(100);

    public ChannelWriter<string> Writer => _channel.Writer;
    public ChannelReader<string> Reader => _channel.Reader;
}

public class MessageQueue
{
    private readonly Channel<MusicMessage> _channel = Channel.CreateBounded<MusicMessage>(100);

    public ChannelWriter<MusicMessage> Writer => _channel.Writer;
    public ChannelReader<MusicMessage> Reader => _channel.Reader;
}
