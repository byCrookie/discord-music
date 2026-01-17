using DiscordMusic.Core.Discord.Actions;
using DiscordMusic.Core.Discord.Interactions;
using DiscordMusic.Core.Discord.Voice;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord;

public static class DiscordModule
{
    public static void AddDiscord(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<DiscordOptions>()
            .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddDiscordGateway(options =>
        {
            options.Token = builder
                .Configuration.GetSection(DiscordOptions.SectionName)
                .GetValue<string>(nameof(DiscordOptions.Token));
        });

        builder.Services.AddGatewayHandler<VoiceStateUpdateHandler>();

        builder.Services.AddApplicationCommands();
        builder.Services.AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();

        builder.Services.AddSingleton<IVoiceHost, VoiceHost>();
    }

    public static void UseDiscord(this IHost host)
    {
        host.AddComponentInteractionModule<AudioBarModule>();

        host.AddApplicationCommandModule<PingAction>();
        host.AddApplicationCommandModule<PlayAction>();
        host.AddApplicationCommandModule<PlayNextAction>();
        host.AddApplicationCommandModule<JoinAction>();
        host.AddApplicationCommandModule<LeaveAction>();
        host.AddApplicationCommandModule<PauseAction>();
        host.AddApplicationCommandModule<ResumeAction>();
        host.AddApplicationCommandModule<SeekAction>();
        host.AddApplicationCommandModule<SkipAction>();
        host.AddApplicationCommandModule<QueueAction>();
        host.AddApplicationCommandModule<NowPlayingAction>();
        host.AddApplicationCommandModule<LyricsAction>();
        host.AddApplicationCommandModule<AudioBarAction>();
    }
}
