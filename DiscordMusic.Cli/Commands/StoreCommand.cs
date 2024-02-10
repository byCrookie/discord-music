using Cocona;
using DiscordMusic.Cli.Commands.Global;
using DiscordMusic.Core.Discord.Music.Store;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Commands;

internal class StoreCommand(IMusicStore store, ILogger<RegisterCommand> logger)
{
    [UsedImplicitly]
    [Command("store")]
    public Task StoreAsync(
        GlobalArguments globalArguments,
        [Option('c', Description = "clear local files in storage")]
        bool clear = false
    )
    {
        if (clear)
        {
            return store.ClearAsync();
        }

        var size = store.GetSize();
        logger.LogInformation("Size: {Size}", size);
        return Task.CompletedTask;
    }
}