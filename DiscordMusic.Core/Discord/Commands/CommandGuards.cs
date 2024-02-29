using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

public static class CommandGuards
{
    public static async Task<bool> IsConnectedToVoiceChannelAsync<T>(ICommandContext context, ILogger<T> logger)
        where T : class
    {
        if (!await IsGuildUserAsync(context, logger))
        {
            return false;
        }

        if (context.User is IGuildUser { VoiceChannel: not null })
        {
            return true;
        }

        await CommandReplies.ErrorAsync(
            context,
            logger,
            "You must be connected to a voice channel.",
            "User {User} is not connected to a voice channel",
            context.User
        );

        return false;
    }

    private static async Task<bool> IsGuildUserAsync<T>(ICommandContext context, ILogger<T> logger)
        where T : class
    {
        if (context.User is IGuildUser)
        {
            return true;
        }

        await CommandReplies.ErrorAsync(
            context,
            logger,
            "You are not a guild user", "User {User} is not a guild user",
            context.User
        );

        return false;
    }
}
