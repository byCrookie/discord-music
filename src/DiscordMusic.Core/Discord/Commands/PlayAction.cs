using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.YouTube.Searching;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class PlayAction(
    ILogger<PlayAction> logger,
    IBackgroundQueue<YouTubeSearchRequest> queue,
    VoiceConnectionService voiceConnectionService
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "play",
        "Request track by direct link or search query.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Play([SlashCommandParameter] string query)
    {
        logger.LogTrace("Play");

        if (Context.Guild is not { } guild)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    DiscordResponses.Ephemeral("The guild is not available. Try again later.")
                )
            );
            return;
        }

        var joinResult = await voiceConnectionService.JoinUserChannelAsync(
            Context.Client,
            guild.Id,
            guild.VoiceStates,
            Context.User.Id
        );

        if (!joinResult.Succeeded)
        {
            await RespondAsync(
                InteractionCallback.Message(DiscordResponses.Ephemeral(joinResult.Message))
            );
            return;
        }

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Embeds =
                    [
                        new EmbedProperties
                        {
                            Title = "Request",
                            Description = BuildRequestMessage(query, joinResult.Status),
                            Color = new Color(red: 0, green: 255, blue: 0),
                        },
                    ],
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );

        var queued = await queue.QueueAsync(_ => new YouTubeSearchRequest(
            query,
            DiscordRequestOrigin.FromContext(Context),
            TrackQueuePlacement.Last
        ));

        if (!queued)
        {
            await FollowupAsync(
                DiscordResponses.Ephemeral("The request queue is full. Try again later.")
            );
        }
    }

    private static string BuildRequestMessage(
        string query,
        VoiceConnectionResultStatus connectionStatus
    )
    {
        return connectionStatus == VoiceConnectionResultStatus.Connected
            ? $"Joined your voice channel and started searching for `{query}`."
            : $"Searching for `{query}`. Matching tracks will be queued first and downloaded only when they are next to play.";
    }
}
