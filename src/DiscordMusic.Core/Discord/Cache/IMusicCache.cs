using System.IO.Abstractions;
using ErrorOr;
using Humanizer.Bytes;

namespace DiscordMusic.Core.Discord.Cache;

public interface IMusicCache
{
    public Task<ErrorOr<IFileInfo>> GetOrAddTrackAsync(Track track, CancellationToken ct);
    public Task<ErrorOr<IFileInfo>> UpdateTrackAsync(Track track, Track updatedTrack, CancellationToken ct);
    public Task<ErrorOr<ByteSize>> GetSizeAsync(CancellationToken ct);
    public Task<ErrorOr<Success>> ClearAsync(CancellationToken ct);
}
