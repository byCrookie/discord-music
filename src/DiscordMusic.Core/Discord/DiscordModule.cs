using DiscordMusic.Core.Audio.Sending;
using DiscordMusic.Core.Audio.Sources;
using DiscordMusic.Core.Discord.Commands;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord;

public static class DiscordModule
{
    extension(IHostApplicationBuilder builder)
    {
        public void AddDiscord()
        {
            builder.AddDiscordOptions();
            builder.AddDiscordGatewayServices();
            builder.AddDiscordCommandServices();
            builder.AddDiscordVoiceServices();
            builder.AddPlaybackServices();
        }

        private void AddDiscordOptions()
        {
            builder
                .Services.AddOptions<DiscordOptions>()
                .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        private void AddDiscordGatewayServices()
        {
            builder.Services.AddDiscordGateway(options =>
            {
                options.Token = builder
                    .Configuration.GetSection(DiscordOptions.SectionName)
                    .GetValue<string>(nameof(DiscordOptions.Token));
                options.Intents =
                    GatewayIntents.Guilds
                    | GatewayIntents.GuildVoiceStates
                    | GatewayIntents.DirectMessages
                    | GatewayIntents.GuildMessagePolls;
            });

            builder.Services.AddGatewayHandler<VoiceStateUpdateHandler>();
        }

        private void AddDiscordCommandServices()
        {
            builder.Services.AddApplicationCommands(o =>
            {
                o.ResultHandler =
                    ApplicationCommandResultHandler<ApplicationCommandContext>.Ephemeral;
            });
            builder.Services.AddComponentInteractions<
                ButtonInteraction,
                ButtonInteractionContext
            >();
            builder.Services.AddSingleton<IDiscordFeedbackService, DiscordFeedbackService>();
        }

        private void AddDiscordVoiceServices()
        {
            builder.Services.AddSingleton<VoiceConnectionRegistry>();
            builder.Services.AddSingleton<VoiceConnectionService>();
        }

        private void AddPlaybackServices()
        {
            builder.Services.AddSingleton<ITrackQueue, TrackQueue>();
            builder.Services.AddSingleton<IPcmAudioSourceFactory, FilePcmAudioSourceFactory>();
            builder.Services.AddSingleton<IAudioSender, TimedAudioSender>();
            builder.Services.AddSingleton<IPlaybackController, PlaybackController>();
            builder.Services.AddSingleton<PlaybackService>();
            builder.Services.AddHostedService(provider =>
                provider.GetRequiredService<PlaybackService>()
            );
        }
    }

    public static void UseDiscord(this IHost host)
    {
        host.AddApplicationCommandModule<PingAction>();
        host.AddApplicationCommandModule<PlayAction>();
        host.AddApplicationCommandModule<JoinAction>();
        host.AddApplicationCommandModule<LeaveAction>();
        host.AddApplicationCommandModule<QueueAction>();
        host.AddApplicationCommandModule<AudioBarAction>();
        host.AddApplicationCommandModule<LyricsAction>();
        host.AddApplicationCommandModule<NowPlayingAction>();
        host.AddApplicationCommandModule<PauseAction>();
        host.AddApplicationCommandModule<PlayNextAction>();
        host.AddApplicationCommandModule<ResumeAction>();
        host.AddApplicationCommandModule<SeekAction>();
        host.AddApplicationCommandModule<SkipAction>();
        host.AddApplicationCommandModule<StopAction>();
        host.AddComponentInteractionModule<AudioBarComponentModule>();
    }
}
