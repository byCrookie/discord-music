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
        var ffmpeg = binaryLocator.LocateAndValidate(options.Ffmpeg, "ffmpeg");
        var deno = binaryLocator.LocateAndValidate(options.Deno, "deno");
        var ytdlp = binaryLocator.LocateAndValidate(options.Ytdlp, "yt-dlp");

        if (!ffmpeg.IsError && !deno.IsError && !ytdlp.IsError)
        {
            lock (_loadLock)
            {
                _value = new YouTubeToolLocationSet(ffmpeg.Value, deno.Value, ytdlp.Value);
            }
        }
        else
        {
            lock (_loadLock)
            {
                _value = null;
            }
        }

        return new YouTubeToolLocationLoadResult(ffmpeg, deno, ytdlp);
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
