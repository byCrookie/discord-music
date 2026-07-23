using System.Diagnostics;
using DiscordMusic.Client.Music;

AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

var rootCommand = new DiscordMusicCommand(args);
return await rootCommand.Parse(args).InvokeAsync();

void UnhandledExceptionHandler(object source, UnhandledExceptionEventArgs args)
{
    var ex = (Exception)args.ExceptionObject;

    var activity = Activity.Current;

    while (activity != null)
    {
        activity.AddException(ex);
        activity.Dispose();
        activity = activity.Parent;
    }
}
