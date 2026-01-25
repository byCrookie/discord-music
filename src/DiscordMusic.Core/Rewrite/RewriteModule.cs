using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;
using JoinAction = DiscordMusic.Core.Rewrite.Actions.JoinAction;
using PlayAction = DiscordMusic.Core.Rewrite.Actions.PlayAction;

namespace DiscordMusic.Core.Rewrite;

public static class RewriteModule
{
    public static void AddRewrite(this IHostApplicationBuilder builder)
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

        // builder.Services.AddGatewayHandler<VoiceStateUpdateHandler>();

        builder.Services.AddApplicationCommands();
        builder.Services.AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();

        builder.Services.AddSingleton<MusicSessionManager>();
    }

    public static void UseRewrite(this IHost host)
    {
        host.AddComponentInteractionModule<AudioBarModule>();
        
        host.AddApplicationCommandModule<PlayAction>();
        host.AddApplicationCommandModule<JoinAction>();
    }
}
