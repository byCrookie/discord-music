using System.IO.Abstractions;
using Cocona;
using DiscordMusic.Cs.Cli.Commands.Global;
using DiscordMusic.Cs.Cli.Discord.Options;
using DiscordMusic.Cs.Cli.Utils;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cs.Cli.Commands;

internal class InitializeCommand(
    IFileSystem fileSystem,
    ILogger<InitializeCommand> logger,
    IOptions<DiscordCsOptions> discordOptions)
{
    private const string GsiFileName = "gamestate_integration_dm.cfg";

    [UsedImplicitly]
    [Command("init")]
    public async Task InitializeAsync(GlobalArguments globalArguments)
    {
        var gsi = await EmbeddedResource.ReadAsync(typeof(InitializeCommand).Assembly, $"Cs.{GsiFileName}");
        var path = fileSystem.Path.Combine(discordOptions.Value.CsCfg, GsiFileName);

        if (!fileSystem.File.Exists(path))
        {
            logger.LogInformation("Writing gamestate integration file to {Path}", path);
            await fileSystem.File.WriteAllTextAsync(path, gsi);
            return;
        }

        logger.LogWarning("Gamestate integration file already exists at {Path}", path);
    }
}