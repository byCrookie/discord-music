using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord;

public readonly record struct DiscordRequestOrigin(
    ulong GuildId,
    ulong UserId,
    ulong ChannelId,
    ulong ApplicationId = 0,
    string? InteractionToken = null
)
{
    public static DiscordRequestOrigin FromContext(ApplicationCommandContext context)
    {
        return new DiscordRequestOrigin(
            GuildId: context.Guild!.Id,
            UserId: context.User.Id,
            ChannelId: context.Channel.Id,
            ApplicationId: context.Interaction.ApplicationId,
            InteractionToken: context.Interaction.Token
        );
    }
}
