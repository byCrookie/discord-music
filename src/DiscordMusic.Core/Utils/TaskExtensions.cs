using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Utils;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger logger)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                logger.LogError(task.Exception, "Error in fire and forget task");
            }

            return;
        }

        _ = FireAndForgetCoreAsync(task, logger);
    }

    private static async Task FireAndForgetCoreAsync(Task task, ILogger logger)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in fire and forget task");
        }
    }
}
