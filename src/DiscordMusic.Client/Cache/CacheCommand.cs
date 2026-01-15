using System.CommandLine;

namespace DiscordMusic.Client.Cache;

public static class CacheCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("cache", "Cache commands") { Hidden = true };
        command.Add(CacheGetOrAddCommand.Create(args));
        command.Add(CacheSizeCommand.Create(args));
        command.Add(CacheClearCommand.Create(args));
        return command;
    }
}
