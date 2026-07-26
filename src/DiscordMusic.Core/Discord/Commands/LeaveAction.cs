using System.Diagnostics;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal sealed class LeaveAction(
    ILogger<LeaveAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService,
    TimeProvider timeProvider
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
        return await DiscordMusicObservability.TrackDiscordCommandAsync(
            "leave",
            Context.Guild?.Id,
            Context.User.Id,
            timeProvider,
            async _ =>
            {
                logger.LogTrace("Leave");

                if (Context.Guild is not { } guild)
                {
                    return DiscordMusicObservability.CommandResult(
                        DiscordResponses.Ephemeral("The guild is not available. Try again later."),
                        "missing_guild"
                    );
                }

                var guildId = guild.Id;

                if (
                    !voiceInstances.Mapping.TryGetValue(guildId, out var voiceInstance)
                    || voiceInstance is null
                )
                {
                    return DiscordMusicObservability.CommandResult(
                        DiscordResponses.Ephemeral(
                            "Not connected to a voice channel in this guild."
                        ),
                        "not_connected"
                    );
                }

                if (
                    voiceInstances.Mapping.TryRemove(
                        item: new KeyValuePair<ulong, VoiceConnection?>(guildId, voiceInstance)
                    )
                )
                {
                    DiscordMusicObservability.RecordVoiceDisconnect(guildId, "command");
                    using var disconnectActivity = DiscordMusicObservability.StartActivity(
                        "discord.voice.disconnect"
                    );
                    DiscordMusicObservability.SetGuildTag(disconnectActivity, guildId);
                    DiscordMusicObservability.SetTag(disconnectActivity, "reason", "command");
                    try
                    {
                        playbackService.Stop(guildId);
                        await voiceInstance.Client.CloseAsync();
                        disconnectActivity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    catch (Exception ex)
                    {
                        disconnectActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        disconnectActivity?.AddException(ex);
                        throw;
                    }
                    finally
                    {
                        voiceInstance.Dispose();

                        await Context.Client.UpdateVoiceStateAsync(
                            new VoiceStateProperties(guildId, null)
                        );
                    }
                }

                return DiscordMusicObservability.CommandResult(
                    DiscordResponses.Ephemeral("Left voice channel.")
                );
            }
        );
    }
}
