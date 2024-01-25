using System.Diagnostics;
using Cocona;
using DiscordMusic.Cli.Commands.Global;
using DiscordMusic.Core.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cli.Commands;

public class RegisterCommand(IOptions<DiscordSecrets> discordSecrets, ILogger<RegisterCommand> logger)
{
    [UsedImplicitly]
    [Command("register")]
    public Task RegisterAsync(GlobalArguments globalArguments)
    {
        var botUrl =
            $"https://discord.com/oauth2/authorize?client_id={discordSecrets.Value.ApplicationId}&scope=bot&permissions=2150632448";
        logger.LogInformation("Opening {BotUrl} to register bot", botUrl);
        Process.Start(new ProcessStartInfo(botUrl) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}