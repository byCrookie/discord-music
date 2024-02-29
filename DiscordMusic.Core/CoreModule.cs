using System.IO.Abstractions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DiscordMusic.Core;

public static class CoreModule
{
    public static void AddCore(this IServiceCollection services)
    {
        services.AddSingleton(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                             | GatewayIntents.GuildVoiceStates
                             | GatewayIntents.GuildMessages
                             | GatewayIntents.MessageContent
        });
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<CommandService>();
        services.AddTransient<IFileSystem, FileSystem>();

        services.AddSerilog();
        services.AddUtils();
        services.AddSerilog();
    }
}
