using Cocona;
using DiscordMusic.Cs.Cli;
using DiscordMusic.Cs.Cli.Commands;
using DiscordMusic.Shared.Configuration;
using DiscordMusic.Shared.Logging;
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

app.AddCommands<InitializeCommand>();
app.AddCommands<RunCommand>();
app.AddCommands<DestroyCommand>();

await app.RunAsync();