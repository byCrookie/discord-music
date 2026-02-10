using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class PlayAction(
    GuildSessionManager guildSessionManager,
    ILogger<PlayAction> logger,
    Cancellation cancellation
) : SafeApplicationCommandModule
{
    [SlashCommand("play", "Play a track. Direct link or search query. Appended to queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Play([SlashCommandParameter] string query)
    {
        logger.LogTrace("Play");

        var session = await guildSessionManager.JoinAsync(
            Context,
            null,
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
                    ### Searching
                    **{query}**
                    -# This may take a moment...
                    """,
                }
            ),
            logger,
            cancellation.CancellationToken
        );

        var play = await session.Value.PlayAsync(query, cancellation.CancellationToken);

        if (play.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = play.ToErrorContent(),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        await SafeModifyResponseAsync(
            m => m.Content = play.Value.ToValueContent(),
            logger,
            cancellation.CancellationToken
        );
    }
}
