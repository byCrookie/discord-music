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
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("resume", "Resume the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Resume()
    {
        logger.LogTrace("Resume");
        
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
        
        var resume = await session.Value.ResumeAsync(cancellation.CancellationToken);

        if (resume.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(resume.ToErrorContent()),
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

        await RespondAsync(
            InteractionCallback.Message(resume.Value.ToValueContent()),
            cancellationToken: cancellation.CancellationToken
        );
    }
}
