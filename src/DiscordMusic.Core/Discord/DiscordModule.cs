using System.Globalization;
using System.Text;
using DiscordMusic.Core.Discord.Actions;
using DiscordMusic.Core.Discord.Interactions;
using DiscordMusic.Core.Discord.Voice;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord;

public static class DiscordModule
{
    public static IHostApplicationBuilder AddDiscord(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<DiscordOptions>()
            .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<DiscordOptions>, ValidateSettingsOptions>()
        );

        builder.Services.AddDiscordGateway(options =>
        {
            options.Token = builder
                .Configuration.GetSection(DiscordOptions.SectionName)
                .GetValue<string>(nameof(DiscordOptions.Token));
            options.Intents =
                GatewayIntents.Guilds
                | GatewayIntents.GuildVoiceStates
                | GatewayIntents.GuildMessages
                | GatewayIntents.MessageContent
                | GatewayIntents.DirectMessages;
        });

        builder.Services.AddGatewayHandler<MessageCreateHandler>();
        builder.Services.AddGatewayHandler<VoiceStateUpdateHandler>();

        builder.Services.AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();

        builder.Services.AddTransient<Replier>();

        builder.Services.AddTransient<IDiscordAction, PingAction>();
        builder.Services.AddTransient<IDiscordAction, JoinAction>();
        builder.Services.AddTransient<IDiscordAction, PlayAction>();
        builder.Services.AddTransient<IDiscordAction, PauseAction>();
        builder.Services.AddTransient<IDiscordAction, SeekAction>();
        builder.Services.AddTransient<IDiscordAction, LeaveAction>();
        builder.Services.AddTransient<IDiscordAction, LyricsAction>();
        builder.Services.AddTransient<IDiscordAction, NowPlayingAction>();
        builder.Services.AddTransient<IDiscordAction, HelpAction>();
        builder.Services.AddTransient<IDiscordAction, PlayNextAction>();
        builder.Services.AddTransient<IDiscordAction, QueueClearAction>();
        builder.Services.AddTransient<IDiscordAction, QueueAction>();
        builder.Services.AddTransient<IDiscordAction, SeekForwardAction>();
        builder.Services.AddTransient<IDiscordAction, SeekBackwardAction>();
        builder.Services.AddTransient<IDiscordAction, SkipAction>();
        builder.Services.AddTransient<IDiscordAction, ShuffleAction>();
        builder.Services.AddTransient<IDiscordAction, ResumeAction>();
        builder.Services.AddTransient<IDiscordAction, AudioBarAction>();

        builder.Services.AddSingleton<IVoiceHost, VoiceHost>();

        return builder;
    }

    public static IHost UseDiscord(this IHost host)
    {
        host.AddComponentInteractionModule<AudioBarModule>();
        return host;
    }

    private sealed class ValidateSettingsOptions : IValidateOptions<DiscordOptions>
    {
        public ValidateOptionsResult Validate(string? name, DiscordOptions options)
        {
            StringBuilder? failure = null;

            if (!int.TryParse(options.Color, NumberStyles.HexNumber, null, out _))
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(DiscordOptions.Color)} {options.Color} is not a valid hex color"
                );
            }

            return failure is not null ? ValidateOptionsResult.Fail(failure.ToString()) : ValidateOptionsResult.Success;
        }
    }
}
