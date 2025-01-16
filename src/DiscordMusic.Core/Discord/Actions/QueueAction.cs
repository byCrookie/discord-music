using System.Text;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class QueueAction(IVoiceHost voiceHost, IReplies replies, ILogger<QueueAction> logger) : IDiscordAction
{
    private const int PageSize = 20;

    public string Long => "queue";

    public string Short => "q";

    public string Help =>
        $"""
         Display the queue. {PageSize} tracks are displayed per page.
         Usage: `queue | queue <page>`
         <page> - The page number to display. Default is 1.
         """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Queue");

        var page = ParsePage(args);

        if (page.IsError)
        {
            return page.Errors;
        }

        var tracks = await voiceHost.QueueAsync(message, ct);

        if (tracks.IsError)
        {
            return tracks.Errors;
        }

        if (tracks.Value.Count == 0)
        {
            await replies.SendWithDeletionAsync(
                message,
                "Queue",
                "The queue is empty",
                IReplies.DefaultDeletionDelay,
                ct
            );
            return Result.Success;
        }

        var pageCount = tracks.Value.Count / PageSize + 1;
        if (page.Value > pageCount)
        {
            await replies.SendWithDeletionAsync(
                message,
                "Queue",
                $"Queue has only {pageCount} pages",
                IReplies.DefaultDeletionDelay,
                ct
            );
            return Result.Success;
        }

        var pageTracks = tracks.Value.Skip((page.Value - 1) * PageSize).Take(PageSize).ToList();

        var queue = new StringBuilder();
        queue.AppendLine($"Page {page.Value}/{pageCount}");
        queue.AppendLine();
        foreach (var (index, track) in pageTracks.Select((track, index) => (index, track)))
        {
            var counter = $"{index + 1}".PadRight(2 + $"{pageTracks.Count}".Length);
            if (track.Duration == TimeSpan.Zero)
            {
                queue.AppendLine($"{counter} {track.Name} - {track.Artists}");
            }
            else
            {
                queue.AppendLine(
                    $"{counter} {track.Name} - {track.Artists} [{track.Duration.HummanizeSecond()}]"
                );
            }
        }

        await replies.SendWithDeletionAsync(message, "Queue", queue.ToString(), TimeSpan.FromMinutes(1), ct);
        return Result.Success;
    }

    private static ErrorOr<int> ParsePage(string[] args)
    {
        if (args.Length == 0)
        {
            return 1;
        }

        if (int.TryParse(args[0], out var page) && page > 0)
        {
            return page;
        }

        return Error.Validation(description: "Invalid page number. Must be a positive number.");
    }
}
