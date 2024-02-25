namespace DiscordMusic.Core.Utils;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        Task.Run(async () => await task);
    }
}
