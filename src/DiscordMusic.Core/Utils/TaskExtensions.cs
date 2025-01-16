using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Utils;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger logger, CancellationToken ct)
    {
        Task.Run(async () =>
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error in fire and forget task");
            }
        }, ct);
    }

    public static void FireAndForget(this ValueTask task, ILogger logger, CancellationToken ct)
    {
        Task.Run(async () =>
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error in fire and forget task");
            }
        }, ct);
    }
}
