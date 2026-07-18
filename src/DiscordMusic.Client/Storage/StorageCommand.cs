using System.CommandLine;

namespace DiscordMusic.Client.Storage;

public sealed class StorageCommand : Command
{
    public StorageCommand(string[] args)
        : base("storage", "Storage commands")
    {
        Add(new StorageSizeCommand(args));
        Add(new StorageClearCommand(args));

        Hidden = true;
    }
}
