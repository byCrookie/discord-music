namespace DiscordMusic.Core.Utils;

public static class TaskExtensions
{
    public static async Task<bool> BoolOrCanceledAsFalseAsync(this ValueTask<bool> task)
    {
        try
        {
            return await task;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}