using Discord.Commands;
using DiscordMusic.Core.Discord.Music.Store;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class StoreCommand(IMusicStore store, ILogger<StoreCommand> logger) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("store")]
    public Task StoreAsync()
    {
        logger.LogTrace("Command store");
        var size = store.GetSize();
        return ReplyAsync($"The store is {size} in size.");
    }
}
