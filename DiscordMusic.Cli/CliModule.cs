using System.IO.Abstractions;
using DiscordMusic.Core;
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
    }
}