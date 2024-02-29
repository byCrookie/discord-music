using Discord.Commands;

namespace DiscordMusic.Cs.Cli.Discord.Commands;

public static class CommandRegistration
{
    public static async Task AddCommandsAsync(CommandService commands, IServiceProvider serviceProvider)
    {
        await commands.AddModuleAsync<PingCommand>(serviceProvider);
        await commands.AddModuleAsync<HelpCommand>(serviceProvider);
        await commands.AddModuleAsync<ListenCommand>(serviceProvider);
        await commands.AddModuleAsync<PlayOnFreezeCommand>(serviceProvider);
    }
}
