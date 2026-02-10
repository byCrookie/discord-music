using System.Text;
using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

[SlashCommand("queue", "Various queue commands.")]
internal class QueueAction(
    GuildSessionManager guildSessionManager,
    ILogger<QueueAction> logger,
    Cancellation cancellation
) : SafeApplicationCommandModule
{
    private const int PageSize = 20;

    [SubSlashCommand("list", "List the tracks in the queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Queue(
        [SlashCommandParameter(Description = "The page number to display. Default is 1.")]
            int page = 1
    )
    {
        logger.LogTrace("Queue");

        if (page <= 0)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = "Invalid page number. It must be 1 or higher.",
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild!.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var tracks = await session.Value.QueueAsync(cancellation.CancellationToken);

        if (tracks.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = tracks.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        if (tracks.Value.Count == 0)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = "The queue is empty.",
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var pageCount = tracks.Value.Count / PageSize + 1;
        if (page > pageCount)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = $"Invalid page number. There are only {pageCount} pages.",
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var pageTracks = tracks.Value.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        var queue = new StringBuilder();
        queue.AppendLine($"Page {page}/{pageCount}");
        queue.AppendLine();
        foreach (var (index, track) in pageTracks.Select((track, index) => (index, track)))
        {
            var counter = $"{index + 1}".PadRight(2 + $"{pageTracks.Count}".Length);
            if (track.Duration == TimeSpan.Zero)
            {
                queue.AppendLine($"{counter} {track.Name} - {track.Artists}");
            }
            else
            {
                queue.AppendLine(
                    $"{counter} {track.Name} - {track.Artists} [{track.Duration.HumanizeSecond()}]"
                );
            }
        }

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Queue
                    {queue}
                    """,
                    Flags = MessageFlags.Ephemeral,
                }
            ),
            logger,
            cancellation.CancellationToken
        );
    }

    [SubSlashCommand("clear", "Clear the queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Clear()
    {
        logger.LogTrace("Queue clear");

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild!.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var clear = await session.Value.QueueClearAsync(cancellation.CancellationToken);

        if (clear.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(clear.ToErrorContent()),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message("Queue cleared."),
            logger,
            cancellation.CancellationToken
        );
    }

    [SubSlashCommand("shuffle", "Shuffle the queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Shuffle()
    {
        logger.LogTrace("Shuffle");

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild!.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var shuffle = await session.Value.ShuffleAsync(cancellation.CancellationToken);

        if (shuffle.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(shuffle.ToErrorContent()),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        if (shuffle.Value.Track is null)
        {
            await SafeRespondAsync(
                InteractionCallback.Message("The queue is empty."),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message(
                $"""
                ### Next
                **{shuffle.Value.Track!.Name}** by **{shuffle.Value.Track!.Artists}** ({shuffle.Value.Track!.Duration.HumanizeSecond()})
                """
            ),
            logger,
            cancellation.CancellationToken
        );
    }
}
