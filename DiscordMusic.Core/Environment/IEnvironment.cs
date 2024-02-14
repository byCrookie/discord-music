using System.IO.Abstractions;

namespace DiscordMusic.Core.Environment;

internal interface IEnvironment
{
    public IDirectoryInfo GetFolderPath(System.Environment.SpecialFolder folder);
}
