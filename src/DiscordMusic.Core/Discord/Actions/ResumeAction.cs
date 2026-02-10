using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class ResumeAction(
    GuildSessionManager guildSessionManager,
    ILogger<ResumeAction> logger,
    Cancellation cancellation
) : SafeApplicationCommandModule
{
    [SlashCommand("resume", "Resume the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Resume()
    {
        logger.LogTrace("Resume");

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

        var resume = await session.Value.ResumeAsync(cancellation.CancellationToken);

        if (resume.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(resume.ToErrorContent()),
                logger,
                cancellation.CancellationToken
            );
            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message(resume.Value.ToValueContent()),
            logger,
            cancellation.CancellationToken
        );
    }
}
