using Cocona;
using DiscordMusic.Cs.Cli;
using DiscordMusic.Cs.Cli.Commands;
using DiscordMusic.Cs.Cli.Configuration;
using DiscordMusic.Cs.Cli.Logging;
using Serilog;

Log.Logger = Logging
    .Initialize(args)
    .CreateLogger();

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Fatal(args.Exception, "Unobserved task exception");
    args.SetObserved();
};

var builder = CoconaApp.CreateBuilder(
    args,
    options => options.EnableShellCompletionSupport = true
);

builder.AddConfiguration();

builder.Services.AddCli(builder.Configuration);

var app = builder.Build();

app.AddCommand(async (CoconaAppContext ctx) =>
{
    if (ctx.CancellationToken.IsCancellationRequested)
    {
        Log.Verbose("Canceled before start.");
        return;
    }
    
    while (!ctx.CancellationToken.IsCancellationRequested)
    {
        Log.Verbose("Not canceled yet.");
        await Task.Delay(100);
    }

    Log.Verbose("Canceled.");
});

app.AddCommands<InitializeCommand>();
app.AddCommands<RunCommand>();
app.AddCommands<DestroyCommand>();

await app.RunAsync();