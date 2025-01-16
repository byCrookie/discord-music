using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using DiscordMusic.Client.Music;

var builder = new CommandLineBuilder(DiscordMusicCommand.Create(args));
builder.UseDefaults();
var parser = builder.Build();
return await parser.InvokeAsync(args);
