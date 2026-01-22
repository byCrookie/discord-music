using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Voice;

public record VoiceHostContext(ulong UserId, ulong? GuildId, ulong ChannelId)
{
    public static VoiceHostContext FromApplicationCommandContext(ApplicationCommandContext context) =>
        new(context.User.Id, context.Guild?.Id, context.Channel.Id);
};
