using Cocona;
using DiscordMusic.Cli;
using DiscordMusic.Cli.Commands;
using DiscordMusic.Cli.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;

Log.Logger = CliLogger
    .Create(args)
    .CreateLogger();

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Fatal(args.Exception, "Unobserved task exception");
};

var builder = CoconaApp.CreateBuilder(
    args,
    options => options.EnableShellCompletionSupport = true
);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddCli(builder.Configuration);

var app = builder.Build();

app.AddCommands<RegisterCommand>();
app.AddCommands<TestCommand>();

await app.RunAsync();