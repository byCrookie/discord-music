using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.VoiceCommands;

public static class VoiceCommandsModule
{
    public static void AddVoiceCommands(this IServiceCollection services)
    {
        services.AddSingleton<SimpleVoiceCommandParser>();
        services.AddSingleton<WhisperVoiceTranscriber>();
        services.AddSingleton<VoiceCommandDispatcher>();
        services.AddSingleton<VoiceCommandManager>();
        services.AddSingleton<VoiceCommandSubscriptions>();
        services.AddHostedService<VoiceCommandService>();
    }
}
