using System.IO.Abstractions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusic.Cli.Data;
using DiscordMusic.Cli.Discord;
using DiscordMusic.Cli.Environment;
using DiscordMusic.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DiscordMusic.Cli;

internal static class CliModule
{
    public static void AddCli(this IServiceCollection services, IConfiguration configuration)
    {
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

        services.AddCore(configuration);
        services.AddDiscord(configuration);
        services.AddEnvironment();
        services.AddData();
    }
}
