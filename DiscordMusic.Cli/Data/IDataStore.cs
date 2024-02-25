using System.IO.Abstractions;

namespace DiscordMusic.Cli.Data;

internal interface IDataStore
{
    IDirectoryInfo Require(string directory);
}
