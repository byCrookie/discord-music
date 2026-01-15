using DiscordMusic.Client.Music;

var rootCommand = DiscordMusicCommand.Create(args);
return await rootCommand.Parse(args).InvokeAsync();
