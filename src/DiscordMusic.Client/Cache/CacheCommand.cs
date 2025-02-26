using System.CommandLine;

namespace DiscordMusic.Client.Cache;

public static class CacheCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("cache", "Cache commands") { IsHidden = true };

        command.AddCommand(CacheGetOrAddCommand.Create(args));
        command.AddCommand(CacheSizeCommand.Create(args));
        command.AddCommand(CacheClearCommand.Create(args));
        return command;
    }
}
