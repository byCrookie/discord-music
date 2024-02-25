using Discord.Commands;

namespace DiscordMusic.Cli.Discord.Commands;

public static class CommandRegistration
{
    public static async Task AddCommandsAsync(CommandService commands, IServiceProvider serviceProvider)
    {
        await commands.AddModuleAsync<PingCommand>(serviceProvider);
        await commands.AddModuleAsync<JoinCommand>(serviceProvider);
        await commands.AddModuleAsync<PlayCommand>(serviceProvider);
        await commands.AddModuleAsync<LeaveCommand>(serviceProvider);
        await commands.AddModuleAsync<PauseCommand>(serviceProvider);
        await commands.AddModuleAsync<SkipCommand>(serviceProvider);
        await commands.AddModuleAsync<NowPlayingCommand>(serviceProvider);
        await commands.AddModuleAsync<QueueCommand>(serviceProvider);
        await commands.AddModuleAsync<HelpCommand>(serviceProvider);
        await commands.AddModuleAsync<BobrCommand>(serviceProvider);
        await commands.AddModuleAsync<QueueClearCommand>(serviceProvider);
        await commands.AddModuleAsync<PlayNextCommand>(serviceProvider);
        await commands.AddModuleAsync<LyricsCommand>(serviceProvider);
        await commands.AddModuleAsync<StoreCommand>(serviceProvider);
    }
}
