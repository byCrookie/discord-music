using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class NowPlayingAction(
    GuildSessionManager guildSessionManager,
    ILogger<NowPlayingAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("nowplaying", "Shows the currently playing track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task NowPlaying()
    {
        logger.LogTrace("Nowplaying");
        
        var session =
            await guildSessionManager.GetSessionAsync(Context.Guild!.Id,
                cancellation.CancellationToken);

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
        
        var nowPlaying = await session.Value.NowPlayingAsync(cancellation.CancellationToken);

        if (nowPlaying.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = nowPlaying.ToErrorContent(),
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
                    Content = nowPlaying.Value.ToValueContent(),
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
