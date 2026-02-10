using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class PauseAction(
    GuildSessionManager guildSessionManager,
    ILogger<PauseAction> logger,
    Cancellation cancellation
) : SafeApplicationCommandModule
{
    [SlashCommand("pause", "Pause the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Pause()
    {
        logger.LogTrace("Pause");

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

        var pause = await session.Value.PauseAsync(cancellation.CancellationToken);

        if (pause.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(pause.ToErrorContent()),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message(pause.Value.ToValueContent()),
            logger,
            cancellation.CancellationToken
        );
    }
}
