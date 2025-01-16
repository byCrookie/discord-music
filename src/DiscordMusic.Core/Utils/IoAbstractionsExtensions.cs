using System.IO.Abstractions;

namespace DiscordMusic.Core.Utils;

public static class IoAbstractionsExtensions
{
    /**
     * Checks if the file exists. Refreshes the file info before checking.
     */
    public static bool Exists(this IFileInfo fileInfo)
    {
        fileInfo.Refresh();
        return fileInfo.Exists;
    }

    /**
     * Checks if the directory exists. Refreshes the directory info before checking.
     */
    public static bool Exists(this IDirectoryInfo directoryInfo)
    {
        directoryInfo.Refresh();
        return directoryInfo.Exists;
    }
}
