using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class PlayNextAction(
    GuildSessionManager guildSessionManager,
    ILogger<PlayNextAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("playnext", "Play a track. Direct link or search query. Prepended to queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task PlayNext(
        [SlashCommandParameter(
            Description = "Direct link or search query. Youtube and Spotify (search only)."
        )]
            string query
    )
    {
        logger.LogTrace("Playnext");

        var session = await guildSessionManager.JoinAsync(
            Context,
            null,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );
            return;
        }

        await RespondAsync(
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
            cancellationToken: cancellation.CancellationToken
        );

        var play = await session.Value.PlayNextAsync(query, cancellation.CancellationToken);

        if (play.IsError)
        {
            await ModifyResponseAsync(m => m.Content = play.ToErrorContent());
            return;
        }

        await ModifyResponseAsync(m => m.Content = play.Value.ToValueContent());
    }
}
