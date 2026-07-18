using DiscordMusic.Client.Music;

var rootCommand = new DiscordMusicCommand(args);
return await rootCommand.Parse(args).InvokeAsync();
