# discord-music

Another music bot for discord. This bot is written in C# and
uses [Discord.Net](https://github.com/discord-net/Discord.Net), [FFmpeg](https://github.com/FFmpeg/FFmpeg), [yt-dlp](https://github.com/yt-dlp/yt-dlp),
[SpotifyApi-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)
and [lyrist](https://github.com/asrvd/lyrist).

## Setup

> Warning: The token should be kept secret and not shared with anyone. If the token is shared, it should be regenerated.

> Note: If you do not want to use the `appsettings.json` file, you can use environment variables prefixed
> with `DISCORD_MUSIC_`. Further information can be found in the [Configuration](#Configuration) section.

### Configuration

The bot uses the `appsettings.json` file for configuration values. If a value is not found in the `appsettings.json`
file it will look for an environment variable prefixed with `DISCORD_MUSIC_`.
Make sure to use double underscores `__` for nested properties. Example: `DISCORD_MUSIC_DISCORD__TOKEN`.
When providing a list, use an indexer `__0` for the first item, `__1` for the second item and so on.
Example: `DISCORD_MUSIC_DISCORD__WHITELIST__0=music`.

### Discord

Go to https://discord.com/developers/applications and create a new application.

Replace the `Discord:ApplicationId` in the `appsettings.json` file with the application id of your new application.
Next replace the `Discord:Token` in the `appsettings.json` file with the token of your new application.

### Spotify (Optional)

Go to https://developer.spotify.com/dashboard/applications and create a new application.

Replace the `Spotify:ClientId` in the `appsettings.json` file with the client
id of your new application. Next replace the `Spotify:ClientSecret` in the `appsettings.json`
file with the client secret of your new application.

### Register

Use the register command to add the bot to a server.

```powershell
dm register
```

## FFmpeg

The bot requires FFmpeg to be installed on the system. Download it
from https://www.ffmpeg.org/download.html and add it to the system path
or place it in the same directory as the bot. If you want to specify the path
explicitly change the `ffmpeg` value in the `appsettings.json` file to the path of
the FFmpeg executable.

```json
{
  "ffmpeg": "C:\\ffmpeg\\bin\\ffmpeg.exe"
}
```

## yt-dlp

The bot requires yt-dlp to be installed on the system. Download it
from https://github.com/yt-dlp/yt-dlp/releases and add it to the system path
or place it in the same directory as the bot. If you want to specify the path
explicitly change the `ytdlp` value in the `appsettings.json` file to the path of
the yt-dlp executable.

```json
{
  "ytdlp": "C:\\yt-dlp\\yt-dlp.exe"
}
```

## Running

Run the bot using the run command.

```powershell
dm run
```

### Logging

Logging can be configured using options on all commands.

```powershell
dm run --verbosity debug --log-file log.txt --quiet false
```

## Other Commands

### Store

To get your local storage size use the store command.

```powershell
dm store
```

#### Clear

To clear your local storage use the store command with the clear option.

```powershell
dm store --clear
```

## Counter Strike

`dmcs` is a command line tool to integrate discord-music with Counter Strike.
Based on the current game round states the bot will play or pause the music.

- [Reddit - GSI](https://www.reddit.com/r/GlobalOffensive/comments/cjhcpy/game_state_integration_a_very_large_and_indepth/)
- [Wiki - GSI](https://developer.valvesoftware.com/wiki/Counter-Strike:_Global_Offensive_Game_State_Integration)
- [Github - rakijah (.NET GSI)](https://github.com/rakijah/CSGSI)

### Initialize

To initialize the Counter Strike integration use the `init` command. It
will create a `gamestate_integration_dm.cfg` file in the `csgo/cfg` directory.

```powershell
dmcs init
```

### Run

To run the Counter Strike integration use the `run` command. It will start
the bot and interact with discord-music based on the current game round states.

```powershell
dmcs run
```

## Lyrics

Thanks [asrvd - lyrist](https://github.com/asrvd/lyrist) for the lyrics api. The bot
uses the lyrics api to get the lyrics of the current song.

## Development

### Secrets

During development environment variables can not be used. Instead use the `dotnet user-secrets` command to set the
secrets
and use the `appsettings.Development.json` for other settings.

#### Token

Token from https://discord.com/developers/applications/1200147726013300866/bot

```powershell
dotnet user-secrets set "Discord:Token" ""
```

#### ApplicationId

ApplicationId from https://discord.com/developers/applications/1200147726013300866/information

```powershell
dotnet user-secrets set "Discord:ApplicationId" ""
```

### Publish

To publish the bot use the `dotnet publish` command. `appsettings.json` will be included in the publish directory but
not overwritten.

```powershell
dotnet publish .\DiscordMusic.Cli\ --output "D:\Apps\Discord\Music\DiscordMusic"
```

#### Runtime

To specify the runtime use the `--runtime` option. Available runtimes can be
found [here](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).

```powershell
dotnet publish .\DiscordMusic.Cli\ --output "D:\Apps\Discord\Music\DiscordMusic" --runtime win-x64
```

### Settings

To change settings use the `appsettings.Development.json` file. This file
is generated once from the `appsettings.Example.json` file during the first build.
The `appsettings.Development.json`is not included in the
repository (`.gitignore`). `applicationId` and `token` are
not included in the `appsettings.Example.json` file and should not
be included in the `appsettings.Development.json` file. Instead use
the `dotnet user-secrets` command to set the secrets.