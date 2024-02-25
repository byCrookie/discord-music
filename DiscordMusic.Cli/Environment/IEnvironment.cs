using System.IO.Abstractions;

namespace DiscordMusic.Cli.Environment;

internal interface IEnvironment
{
    public IDirectoryInfo GetFolderPath(System.Environment.SpecialFolder folder);
}
