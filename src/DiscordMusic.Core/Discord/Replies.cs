using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord;

public class Replies(RestClient restClient, IOptions<DiscordOptions> options) : IReplies
{
    public async Task SendDmAsync(Message message, string title, string? description, CancellationToken ct)
    {
        var dm = await message.Author.GetDMChannelAsync(cancellationToken: ct);
        await dm.SendMessageAsync(
            new MessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Color = options.Value.DiscordColor,
                        Title = title,
                        Description = description
                    }
                ]
            },
            cancellationToken: ct
        );
    }

    public Task SendAsync(Message message, string title, string? description, CancellationToken ct)
    {
        return message.ReplyAsync(
            new ReplyMessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Color = options.Value.DiscordColor,
                        Title = title,
                        Description = description
                    }
                ]
            },
            cancellationToken: ct
        );
    }

    public async Task SendWithDeletionAsync(
        Message message,
        string title,
        string? description,
        TimeSpan deletionDelay,
        CancellationToken ct
    )
    {
        var reply = await message.ReplyAsync(
            new ReplyMessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Color = options.Value.DiscordColor,
                        Title = title,
                        Description = description
                    }
                ]
            },
            cancellationToken: ct
        );

        await DeleteNonBlockingAfterAsync(reply, deletionDelay, ct);
    }

    public async Task SendWithDeletionAsync(
        ulong channelId,
        string title,
        string? description,
        TimeSpan deletionDelay,
        CancellationToken ct
    )
    {
        var reply = await restClient.SendMessageAsync(
            channelId,
            new MessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Color = options.Value.DiscordColor,
                        Title = title,
                        Description = description
                    }
                ]
            },
            cancellationToken: ct
        );

        await DeleteNonBlockingAfterAsync(reply, deletionDelay, ct);
    }

    public async Task SendErrorWithDeletionAsync(
        Message message,
        string? content,
        TimeSpan deletionDelay,
        CancellationToken ct
    )
    {
        var reply = await message.ReplyAsync(
            new ReplyMessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Color = new Color(255, 0, 0),
                        Title = "Error",
                        Description = content
                    }
                ]
            },
            cancellationToken: ct
        );

        await DeleteNonBlockingAfterAsync(reply, deletionDelay, ct);
    }

    public async Task SendErrorWithDeletionAsync(
        ulong channelId,
        string? content,
        TimeSpan deletionDelay,
        CancellationToken ct
    )
    {
        var reply = await restClient.SendMessageAsync(
            channelId,
            new MessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Color = new Color(255, 0, 0),
                        Title = "Error",
                        Description = content
                    }
                ]
            },
            cancellationToken: ct
        );

        await DeleteNonBlockingAfterAsync(reply, deletionDelay, ct);
    }

    private static Task DeleteNonBlockingAfterAsync(RestMessage message, TimeSpan deletionDelay, CancellationToken ct)
    {
        _ = Task.Factory.StartNew(
            async () =>
            {
                await Task.Delay(deletionDelay, ct);
                await message.DeleteAsync(cancellationToken: ct);
            },
            ct
        );

        return Task.CompletedTask;
    }
}
