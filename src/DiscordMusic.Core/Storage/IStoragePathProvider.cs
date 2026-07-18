using System.IO.Abstractions;

namespace DiscordMusic.Core.Storage;

public interface IStoragePathProvider
{
    IDirectoryInfo StorageDirectory();
}
