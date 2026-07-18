using System.Text;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

[SlashCommand("queue", "Various queue commands.", Contexts = [InteractionContextType.Guild])]
internal class QueueAction(
    ILogger<QueueAction> logger,
    ITrackQueue trackQueue,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService
) : ApplicationCommandModule<ApplicationCommandContext>
{
    private const int PageSize = 20;

    [SubSlashCommand("list", "List the tracks in the queue.")]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties Queue(
        [SlashCommandParameter(Description = "The page number to display. Default is 1.")]
            int page = 1
    )
    {
        logger.LogTrace("Queue");

        if (page <= 0)
        {
            return DiscordResponses.Ephemeral("Invalid page number. It must be 1 or higher.");
        }

        if (Context.Guild is not { } guild)
        {
            return DiscordResponses.Ephemeral("The guild is not available. Try again later.");
        }

        var queuedTracks = trackQueue.QueuedTracks(guild.Id);
        var playbackProgress = TryRenderPlaybackProgress(guild.Id, out var progress)
            ? $"{progress}\n\n"
            : string.Empty;

        if (queuedTracks.Count == 0)
        {
            if (playbackProgress.Length == 0)
            {
                return DiscordResponses.Ephemeral("The queue is empty.");
            }

            return DiscordResponses.Public(
                $"""
                ### Queue
                {playbackProgress}No upcoming tracks queued.
                """
            );
        }

        var pageCount = (queuedTracks.Count + PageSize - 1) / PageSize;
        if (page > pageCount)
        {
            return DiscordResponses.Ephemeral(
                $"Invalid page number. There are only {pageCount} pages."
            );
        }

        var pageTracks = queuedTracks.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        var queue = new StringBuilder();
        queue.AppendLine($"Page {page}/{pageCount}");
        queue.AppendLine();
        foreach (var (index, track) in pageTracks.Select((track, index) => (index, track)))
        {
            var counter = $"{index + 1}".PadRight(2 + $"{pageTracks.Count}".Length);
            if (track.Track.Duration == TimeSpan.Zero)
            {
                queue.AppendLine(
                    $"{counter} {track.Track.Name} - {track.Track.Artists} ({Status(track)})"
                );
            }
            else
            {
                queue.AppendLine(
                    $"{counter} {track.Track.Name} - {track.Track.Artists} [{track.Track.Duration.HumanizeSecond()}] ({Status(track)})"
                );
            }
        }

        return DiscordResponses.Public(
            $"""
            ### Queue
            {playbackProgress}{queue}
            """
        );
    }

    private bool TryRenderPlaybackProgress(ulong guildId, out string progress)
    {
        PlaybackSession session;
        if (
            voiceInstances.Mapping.TryGetValue(guildId, out var voiceConnection)
            && voiceConnection is not null
        )
        {
            session = voiceConnection.PlaybackSession;
        }
        else if (!playbackService.TryGetPlaybackSession(guildId, out session))
        {
            progress = string.Empty;
            return false;
        }

        var snapshot = session.Snapshot();
        if (snapshot.CurrentTrack is null)
        {
            progress = string.Empty;
            return false;
        }

        progress = AudioBarRenderer.Render(snapshot, mode: AudioBarRenderMode.Inline);
        return true;
    }

    private static string Status(QueuedTrack track)
    {
        return track.Status switch
        {
            QueuedTrackStatus.Pending => "Queued",
            QueuedTrackStatus.Downloading => "Downloading",
            QueuedTrackStatus.Available => "Available",
            QueuedTrackStatus.Failed => "Failed",
            _ => "Unknown",
        };
    }

    [SubSlashCommand("clear", "Clear the queue.")]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties Clear(
        [SlashCommandParameter(Description = "Whether to clear only failed tracks.")]
            bool failedOnly = false
    )
    {
        logger.LogTrace("Queue clear");

        if (Context.Guild is not { } guild)
        {
            return DiscordResponses.Ephemeral("The guild is not available. Try again later.");
        }

        if (failedOnly)
        {
            trackQueue.ClearFailedOnly(guild.Id);
            return DiscordResponses.Ephemeral("Queue cleared of failed tracks.");
        }

        trackQueue.Clear(guild.Id);
        return DiscordResponses.Ephemeral("Queue cleared.");
    }

    [SubSlashCommand("shuffle", "Shuffle the queue.")]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties Shuffle()
    {
        logger.LogTrace("Shuffle");
        if (Context.Guild is not { } guild)
        {
            return DiscordResponses.Ephemeral("The guild is not available. Try again later.");
        }

        trackQueue.Shuffle(guild.Id);
        return DiscordResponses.Ephemeral("Queue shuffled.");
    }
}
