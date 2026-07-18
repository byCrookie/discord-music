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

internal class PlayNextAction(
    ILogger<PlayNextAction> logger,
    IBackgroundQueue<YouTubeSearchRequest> queue,
    VoiceConnectionService voiceConnectionService
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "playnext",
        "Request a track and place it next in the queue.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task PlayNext([SlashCommandParameter] string query)
    {
        logger.LogTrace("PlayNext");

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
                new InteractionMessageProperties()
                    .WithContent(BuildRequestMessage(query, joinResult.Status))
                    .WithFlags(MessageFlags.Ephemeral)
            )
        );

        var queued = await queue.QueueAsync(_ => new YouTubeSearchRequest(
            query,
            DiscordRequestOrigin.FromContext(Context),
            TrackQueuePlacement.Next
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
            ? $"Joined your voice channel and started searching for `{query}` to queue next."
            : $"Searching for `{query}` to queue next. It will download only when it is next to play.";
    }
}
