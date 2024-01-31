using Cocona;
using DiscordMusic.Cli;
using DiscordMusic.Cli.Commands;
using DiscordMusic.Cli.Configuration;
using DiscordMusic.Cli.Logging;
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

app.AddCommands<RegisterCommand>();
app.AddCommands<RunCommand>();
app.AddCommands<StoreCommand>();

await app.RunAsync();