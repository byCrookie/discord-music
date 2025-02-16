using DiscordMusic.Core.Discord.Interactions;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord;

public class Replier(RestClient restClient, IOptions<DiscordOptions> options)
{
    private ulong? _channelId;
    private Message? _message;
    private string? _content;
    private Color _color = options.Value.DiscordColor;
    private TimeSpan? _deletionDelay;
    private readonly List<ComponentProperties> _components = [];
    private readonly List<EmbedProperties> _embeds = [];
    private bool _isDirectMessage;

    public Replier ReplyTo(Message message)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
        return this;
    }

    public Replier DirectMessage(Message message)
    {
        _isDirectMessage = true;
        _message = message ?? throw new ArgumentNullException(nameof(message));
        return this;
    }

    public Replier ReplyTo(ulong channelId)
    {
        if (channelId == 0)
        {
            throw new ArgumentException("Channel ID cannot be zero.", nameof(channelId));
        }

        _channelId = channelId;
        return this;
    }

    public Replier WithEmbed(string title, string? content, Color? color = null)
    {
        var embed = new EmbedProperties { Color = color ?? _color, Title = title, Description = content };
        _embeds.Add(embed);
        return this;
    }

    public Replier WithContent(string? content)
    {
        _content = content;
        return this;
    }

    public Replier WithColor(Color color)
    {
        _color = color;
        return this;
    }

    public Replier WithDeletion(TimeSpan? delay = null)
    {
        if (delay is null)
        {
            _deletionDelay = TimeSpan.FromSeconds(30);
            return this;
        }

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Deletion delay must be non-negative.");
        }

        _deletionDelay = delay.Value;
        return this;
    }

    public Replier WithAudioBar()
    {
        _components.Add(AudioBar.Create());
        return this;
    }

    public async Task SendAsync(CancellationToken ct)
    {
        if (_isDirectMessage)
        {
            if (_message is null)
            {
                throw new InvalidOperationException("Message must be provided for direct message.");
            }

            var dm = await _message.Author.GetDMChannelAsync(cancellationToken: ct);
            var reply = await dm.SendMessageAsync(new MessageProperties
            {
                Content = _content,
                Embeds = _embeds,
                Components = _components
            }, cancellationToken: ct);

            if (_deletionDelay.HasValue)
            {
                await DeleteNonBlockingAfterAsync(reply, _deletionDelay.Value, ct);
            }

            return;
        }

        if (_message == null && !_channelId.HasValue)
        {
            throw new InvalidOperationException("Either a message or a channel ID must be provided.");
        }

        if (_message is not null)
        {
            var reply = await _message.ReplyAsync(new ReplyMessageProperties
            {
                Content = _content,
                Embeds = _embeds,
                Components = _components
            }, cancellationToken: ct);

            if (_deletionDelay.HasValue)
            {
                await DeleteNonBlockingAfterAsync(reply, _deletionDelay.Value, ct);
            }

            return;
        }

        if (_channelId.HasValue)
        {
            var reply = await restClient.SendMessageAsync(_channelId.Value, new MessageProperties
            {
                Content = _content,
                Embeds = _embeds,
                Components = _components
            }, cancellationToken: ct);

            if (_deletionDelay.HasValue)
            {
                await DeleteNonBlockingAfterAsync(reply, _deletionDelay.Value, ct);
            }
        }
    }

    private static Task DeleteNonBlockingAfterAsync(RestMessage message, TimeSpan deletionDelay, CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(deletionDelay, ct);
            await message.DeleteAsync(cancellationToken: ct);
        }, ct);
        
        return Task.CompletedTask;
    }
}

public static class RepliesBuilderExtensions
{
    public static Task SendErrorAsync(this Replier replier, string? content, CancellationToken ct)
    {
        replier.WithEmbed("Error", content, new Color(255, 0, 0));
        replier.WithDeletion();
        return replier.SendAsync(ct);
    }
}
