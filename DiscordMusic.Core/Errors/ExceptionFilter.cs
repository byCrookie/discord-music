using Cocona.Filters;
using Serilog;

namespace DiscordMusic.Core.Errors;

public class ExceptionFilter : CommandFilterAttribute
{
    public override async ValueTask<int> OnCommandExecutionAsync(CoconaCommandExecutingContext ctx,
        CommandExecutionDelegate next)
    {
        Log.Verbose($"Start Command: {ctx.Command.Name}");
        try
        {
            return await next(ctx);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled Exception");
            return 1;
        }
        finally
        {
            Log.Verbose($"End Command: {ctx.Command.Name}");
        }
    }
}
