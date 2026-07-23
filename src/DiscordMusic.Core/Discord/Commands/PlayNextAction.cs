using System.Diagnostics;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
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
        var startedAt = Stopwatch.GetTimestamp();
        var guildId = Context.Guild?.Id;
        var result = "completed";
        using var activity = DiscordMusicObservability.StartDiscordCommandActivity(
            "playnext",
            guildId,
            Context.User.Id
        );
        DiscordMusicObservability.SetTag(activity, "music.search.query.length", query.Length);

        logger.LogTrace("PlayNext");

        try
        {
            if (Context.Guild is not { } guild)
            {
                result = "missing_guild";
                activity?.SetStatus(ActivityStatusCode.Ok, result);
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
                result = "voice_join_failed";
                activity?.SetStatus(ActivityStatusCode.Ok, result);
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
                result = "queue_full";
                activity?.SetStatus(ActivityStatusCode.Ok, result);
                await FollowupAsync(
                    DiscordResponses.Ephemeral("The request queue is full. Try again later.")
                );
                return;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            var tags = DiscordMusicObservability.DiscordCommandTags("playnext", result, guildId);
            DiscordMusicObservability.DiscordCommands.Add(1, tags);
            DiscordMusicObservability.DiscordCommandDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                tags
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
