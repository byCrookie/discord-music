using Cocona;
using DiscordMusic.Watch.Cli;
using DiscordMusic.Watch.Cli.Commands;
using DiscordMusic.Watch.Cli.Configuration;
using DiscordMusic.Watch.Cli.Logging;
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

app.AddCommands<WatchCommand>();

await app.RunAsync();