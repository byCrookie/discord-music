using System.IO.Abstractions;
using Cocona;
using DiscordMusic.Core.Errors;
using DiscordMusic.Core.Global;
using DiscordMusic.Cs.Cli.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cs.Cli.Commands;

internal class DestroyCommand(
    IFileSystem fileSystem,
    ILogger<DestroyCommand> logger,
    IOptions<CsOptions> csOptions)
{
    private const string GsiFileName = "gamestate_integration_dm.cfg";

    [UsedImplicitly]
    [ExceptionFilter]
    [Command("destroy", Description = "Remove gamestate integration file from the client (where cs is running).")]
    public Task InitializeAsync(GlobalArguments globalArguments)
    {
        var path = fileSystem.Path.Combine(csOptions.Value.Cfg, GsiFileName);

        if (fileSystem.File.Exists(path))
        {
            logger.LogInformation("Removing gamestate integration file from {Path}", path);
            fileSystem.File.Delete(path);
            return Task.CompletedTask;
        }

        logger.LogWarning("Gamestate integration file does not exist at {Path}", path);
        return Task.CompletedTask;
    }
}
