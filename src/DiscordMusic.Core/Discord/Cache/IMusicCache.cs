using System.IO.Abstractions;
using ErrorOr;
using Humanizer;

namespace DiscordMusic.Core.Discord.Cache;

public interface IMusicCache
{
    public Task<ErrorOr<IFileInfo>> GetOrAddTrackAsync(Track track, ByteSize approxSize, CancellationToken ct);

    public Task<ErrorOr<IFileInfo>> AddOrUpdateTrackAsync(Track track, Track updatedTrack, ByteSize approxSize,
        CancellationToken ct);

    public Task<ErrorOr<ByteSize>> GetSizeAsync(CancellationToken ct);
    public Task<ErrorOr<Success>> ClearAsync(CancellationToken ct);
}
