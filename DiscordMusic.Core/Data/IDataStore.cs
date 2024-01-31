using System.IO.Abstractions;

namespace DiscordMusic.Core.Data;

internal interface IDataStore
{
    IDirectoryInfo Require(string directory);
}