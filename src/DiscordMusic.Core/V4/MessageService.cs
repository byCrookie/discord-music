using System.Threading.Channels;
using DiscordMusic.Core.Discord;

namespace DiscordMusic.Core.V4;

public class MessageService
{
    private readonly Channel<Reply> _messageChannel = Channel.CreateUnbounded<Reply>();

    public ChannelWriter<Reply> Messages => _messageChannel.Writer;
}
