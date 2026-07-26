using System.Diagnostics;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Utils;
using ErrorOr;

namespace DiscordMusic.Core.YouTube;

internal sealed class YouTubeToolLocations(BinaryLocator binaryLocator)
{
    private readonly Lock _loadLock = new();
    private YouTubeToolLocationSet? _value;

    public YouTubeToolLocationSet Value
    {
        get
        {
            lock (_loadLock)
            {
                return _value
                    ?? throw new InvalidOperationException(
                        "YouTube tool locations have not been loaded."
                    );
            }
        }
    }

    public YouTubeToolLocationLoadResult Load(YouTubeOptions options)
    {
        using var activity = DiscordMusicObservability.StartActivity("youtube.tools.load");
        var result = "completed";

        try
        {
            var ffmpeg = binaryLocator.LocateAndValidate(options.Ffmpeg, "ffmpeg");
            var deno = binaryLocator.LocateAndValidate(options.Deno, "deno");
            var ytdlp = binaryLocator.LocateAndValidate(options.Ytdlp, "yt-dlp");

            if (!ffmpeg.IsError && !deno.IsError && !ytdlp.IsError)
            {
                lock (_loadLock)
                {
                    _value = new YouTubeToolLocationSet(ffmpeg.Value, deno.Value, ytdlp.Value);
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                result = "failed";
                activity?.SetStatus(ActivityStatusCode.Error, result);
                lock (_loadLock)
                {
                    _value = null;
                }
            }

            DiscordMusicObservability.SetTag(
                activity,
                "youtube.tools.ffmpeg.result",
                Result(ffmpeg)
            );
            DiscordMusicObservability.SetTag(activity, "youtube.tools.deno.result", Result(deno));
            DiscordMusicObservability.SetTag(activity, "youtube.tools.ytdlp.result", Result(ytdlp));
            return new YouTubeToolLocationLoadResult(ffmpeg, deno, ytdlp);
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
            DiscordMusicObservability.SetTag(activity, "result", result);
            DiscordMusicObservability.YouTubeToolLoads.Add(
                1,
                new KeyValuePair<string, object?>("result", result)
            );
        }
    }

    private static string Result(ErrorOr<BinaryLocator.BinaryLocation> location)
    {
        return location.IsError ? "failed" : location.Value.Type.ToString().ToLowerInvariant();
    }
}

internal sealed record YouTubeToolLocationSet(
    BinaryLocator.BinaryLocation Ffmpeg,
    BinaryLocator.BinaryLocation Deno,
    BinaryLocator.BinaryLocation Ytdlp
);

internal sealed record YouTubeToolLocationLoadResult(
    ErrorOr<BinaryLocator.BinaryLocation> Ffmpeg,
    ErrorOr<BinaryLocator.BinaryLocation> Deno,
    ErrorOr<BinaryLocator.BinaryLocation> Ytdlp
);
