namespace DiscordMusic.Core.Storage;

public interface IStorageCacheTrimmer
{
    Task TrimAsync(string storagePath, long maxBytes, CancellationToken cancellationToken);
}
