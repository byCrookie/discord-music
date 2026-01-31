using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class LeaveAction(
    GuildSessionManager guildSessionManager,
    ILogger<LeaveAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("leave", "The bot will leave the voice channel. Deletes guild session.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Leave()
    {
        logger.LogTrace("Leave");

        var leave = await guildSessionManager.LeaveAsync(
            Context.Guild!.Id,
            cancellation.CancellationToken
        );

        if (leave.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = leave.ToErrorContent(),
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
                    Content = """
                    ### Disconnected
                    I left the voice channel.
                    """,
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
