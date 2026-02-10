using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

[SlashCommand("seek", "Seek within the current track.")]
internal class SeekAction(
    GuildSessionManager guildSessionManager,
    ILogger<SeekAction> logger,
    Cancellation cancellation
) : SafeApplicationCommandModule
{
    [SubSlashCommand("position", "Seek to a specific time in the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Seek(
        [SlashCommandParameter(
            Description = "The position to seek to (e.g. hh:mm:ss). Precision is in seconds."
        )]
            string position
    )
    {
        if (!TimeSpanParser.TryParse(position, out var positionTs))
        {
            await SafeRespondAsync(
                InteractionCallback.Message("Not a valid position. Example: 00:01:23"),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        logger.LogTrace("Seeking to {Position}", positionTs);

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

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Seeking to {positionTs.HumanizeSecond()}...
                    -# This may take a moment...
                    """,
                }
            ),
            logger,
            cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            positionTs,
            AudioStream.SeekMode.Position,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
            Seeked to {positionTs.HumanizeSecond()}
            {seek.Value.ToValueContent()}
            """;

        await SafeModifyResponseAsync(
            m => m.Content = seekedMessage,
            logger,
            cancellation.CancellationToken
        );
    }

    [SubSlashCommand("backward", "Seek backward by a duration in the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task SeekBackward(
        [SlashCommandParameter(Description = "The duration to seek backward (e.g. 00:00:10).")]
            string duration
    )
    {
        if (!TimeSpanParser.TryParse(duration, out var durationTs))
        {
            await SafeRespondAsync(
                InteractionCallback.Message("Not a valid duration. Example: 00:00:10"),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        logger.LogTrace("Seeking backward by {Duration}", durationTs);

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

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Seeking backward by {durationTs.HumanizeSecond()}...
                    -# This may take a moment...
                    """,
                }
            ),
            logger,
            cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            durationTs,
            AudioStream.SeekMode.Backward,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
            Seeked backward by {durationTs.HumanizeSecond()}
            {seek.Value.ToValueContent()}
            """;

        await SafeModifyResponseAsync(
            m => m.Content = seekedMessage,
            logger,
            cancellation.CancellationToken
        );
    }

    [SubSlashCommand("forward", "Seek forward by a duration in the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task SeekForward(
        [SlashCommandParameter(Description = "The duration to seek forward (e.g. 00:00:10).")]
            string duration
    )
    {
        if (!TimeSpanParser.TryParse(duration, out var durationTs))
        {
            await SafeRespondAsync(
                InteractionCallback.Message("Not a valid duration. Example: 00:00:10"),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        logger.LogTrace("Seeking foward by {Duration}", durationTs);

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

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Seeking forward by {durationTs.HumanizeSecond()}...
                    -# This may take a moment...
                    """,
                }
            ),
            logger,
            cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            durationTs,
            AudioStream.SeekMode.Forward,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
            Seeked forward by {durationTs.HumanizeSecond()}
            {seek.Value.ToValueContent()}
            """;

        await SafeModifyResponseAsync(
            m => m.Content = seekedMessage,
            logger,
            cancellation.CancellationToken
        );
    }
}
