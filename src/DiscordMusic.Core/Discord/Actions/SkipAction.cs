using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class SkipAction(
    GuildSessionManager guildSessionManager,
    ILogger<SkipAction> logger,
    Cancellation cancellation
) : SafeApplicationCommandModule
{
    [SlashCommand(
        "skip",
        "Skip to a specific track in the queue. If no position is provided, the next track will be played."
    )]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Skip(
        [SlashCommandParameter(
            Description = "The position of the track to skip to (1-based). Omit to skip to next."
        )]
            int? position = null
    )
    {
        logger.LogTrace("Skip");

        var skipCount = 0;

        if (position is not null)
        {
            if (position.Value < 1)
            {
                await SafeRespondAsync(
                    InteractionCallback.Message("Invalid position. It must be 1 or higher."),
                    logger,
                    cancellation.CancellationToken
                );
                return;
            }

            skipCount = position.Value - 1;
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

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Skipping…
                    -# Skipping {skipCount + 1} track(s). This may take a moment...
                    """,
                }
            ),
            logger,
            cancellation.CancellationToken
        );

        var skip = await session.Value.SkipAsync(skipCount, cancellation.CancellationToken);

        if (skip.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = skip.ToErrorContent(),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        await SafeModifyResponseAsync(
            m => m.Content = skip.Value.ToValueContent(),
            logger,
            cancellation.CancellationToken
        );
    }
}
