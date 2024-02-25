using System.Diagnostics;
using Cocona;
using DiscordMusic.Cli.Discord.Options;
using DiscordMusic.Core.Errors;
using DiscordMusic.Core.Global;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cli.Commands;

internal class RegisterCommand(IOptions<DiscordOptions> discordOptions, ILogger<RegisterCommand> logger)
{
    [UsedImplicitly]
    [ExceptionFilter]
    [Command("register")]
    public Task RegisterAsync(GlobalArguments globalArguments)
    {
        var botUrl =
            $"https://discord.com/oauth2/authorize?client_id={discordOptions.Value.ApplicationId}&scope=bot&permissions=2150632448";
        logger.LogInformation("Opening {BotUrl} to register bot", botUrl);
        Process.Start(new ProcessStartInfo(botUrl) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
