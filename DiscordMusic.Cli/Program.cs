using Cocona;
using DiscordMusic.Cli;
using DiscordMusic.Cli.Commands;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Logging;
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

builder.AddConfiguration(typeof(Program).Assembly);

builder.Services.AddCli(builder.Configuration);

var app = builder.Build();

app.AddCommands<RegisterCommand>();
app.AddCommands<RunCommand>();
app.AddCommands<StoreCommand>();

await app.RunAsync();
