using System.IO.Abstractions;
using Cocona;
using DiscordMusic.Cs.Cli.Discord.Options;
using DiscordMusic.Shared.Errors;
using DiscordMusic.Shared.Global;
using DiscordMusic.Shared.Utils;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cs.Cli.Commands;

internal class InitializeCommand(
    IFileSystem fileSystem,
    ILogger<InitializeCommand> logger,
    IOptions<CsOptions> csOptions)
{
    private const string GsiFileName = "gamestate_integration_dm.cfg";

    [UsedImplicitly]
    [ExceptionFilter]
    [Command("init",
        Description = "Initialize the client (where cs is running) by creating a gamestate integration file.")]
    public async Task InitializeAsync(
        GlobalArguments globalArguments,
        [Option('a', Description = "Address of the dmcs server. Default is http://localhost:3000")]
        string address = "http://localhost:3000",
        [Option('t',
            Description =
                "Token to identify the client. Has to be whitelisted by the dmcs server. Default is no token, only works if the server has not whitelisted any tokens.")]
        string token = "",
        [Option('f', Description = "Force overwrite existing configurations.")]
        bool force = false,
        [Option('d', Description = "Diff between the current and the new configuration. Disables other options.")]
        bool diff = false)
    {
        var gsiTemplate = await EmbeddedResource.ReadAsync(typeof(InitializeCommand).Assembly, $"Cs.{GsiFileName}");
        var path = fileSystem.Path.Combine(csOptions.Value.Cfg, GsiFileName);
        
        if (diff)
        {
            if (!fileSystem.File.Exists(path))
            {
                logger.LogInformation("Current gamestate integration does not exist.");
            }
            else
            {
                var currentGsi = await fileSystem.File.ReadAllTextAsync(path);
                logger.LogInformation("Current gamestate integration file: {Current}", currentGsi);
            }

            logger.LogInformation("New gamestate integration file: {New}", gsiTemplate.Replace("{{address}}", address).Replace("{{token}}", token));
            return;
        }

        if (!fileSystem.File.Exists(path) || force)
        {
            logger.LogInformation("Writing gamestate integration file to {Path}", path);
            var gsiContent = gsiTemplate.Replace("{{address}}", address).Replace("{{token}}", token);
            await fileSystem.File.WriteAllTextAsync(path, gsiContent);
            return;
        }

        logger.LogWarning("Gamestate integration file already exists at {Path}", path);
    }
}