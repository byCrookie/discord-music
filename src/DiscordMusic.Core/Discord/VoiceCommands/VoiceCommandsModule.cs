using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.VoiceCommands;

public static class VoiceCommandsModule
{
    public static IServiceCollection AddVoiceCommands(this IServiceCollection services)
    {
        services.AddSingleton<IVoiceCommandParser, SimpleVoiceCommandParser>();
        services.AddSingleton<IVoiceTranscriber, WhisperVoiceTranscriber>();
        services.AddSingleton<VoiceCommandDispatcher>();
        return services;
    }
}
