using System.IO.Abstractions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusic.Core;
using DiscordMusic.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DiscordMusic.Cli;

internal static class CliModule
{
    public static void AddCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCore(configuration);
        services.AddSerilog();
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddSingleton(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                             | GatewayIntents.GuildVoiceStates
                             | GatewayIntents.GuildMessages
                             | GatewayIntents.MessageContent
        });
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<CommandService>();
        services.AddShared();
    }
}