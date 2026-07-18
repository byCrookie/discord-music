using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class LeaveAction(
    ILogger<LeaveAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "leave",
        "Make the bot leave the voice channel.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task<InteractionMessageProperties> Leave()
    {
        logger.LogTrace("Leave");

        if (Context.Guild is not { } guild)
        {
            return DiscordResponses.Ephemeral("The guild is not available. Try again later.");
        }

        var guildId = guild.Id;

        if (
            !voiceInstances.Mapping.TryGetValue(guildId, out var voiceInstance)
            || voiceInstance is null
        )
        {
            return DiscordResponses.Ephemeral("Not connected to a voice channel in this guild.");
        }

        if (
            voiceInstances.Mapping.TryRemove(
                item: new KeyValuePair<ulong, VoiceConnection?>(guildId, voiceInstance)
            )
        )
        {
            try
            {
                playbackService.Stop(guildId);
                await voiceInstance.Client.CloseAsync();
            }
            finally
            {
                voiceInstance.Dispose();

                await Context.Client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
            }
        }

        return DiscordResponses.Ephemeral("Left voice channel.");
    }
}
