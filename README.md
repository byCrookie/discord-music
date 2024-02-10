# discord-music

Another music bot for discord. This bot is written in C#
and uses the [Discord.Net](https://github.com/discord-net/Discord.Net) library,
[FFmpeg](https://github.com/FFmpeg/FFmpeg) and [yt-dlp](https://github.com/yt-dlp/yt-dlp).

## Setup

Go to https://discord.com/developers/applications and create a new application.

Replace the `ApplicationId` in the `appsettings.json` file with the application id of your new application.
Next replace the `Token` in the `appsettings.json` file with the token of your new application.

> Warning: The token should be kept secret and not shared with anyone. If the token is shared, it should be regenerated.

> Note: If you dont want to use the `appsettings.json` file, you can use environment variables prefixed
> with `DISCORD_MUSIC_`.

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

To publish the bot use the `dotnet publish` command.

```powershell
dotnet publish .\DiscordMusic.Cli\ --output "D:\Apps\Discord\Music\DiscordMusic"
```

`appsettings.json` will be included in the publish directory but not overwritten.

### Settings

To change settings use the `appsettings.Development.json` file. This file
is generated once from the `appsettings.Example.json` file during the first build.
The `appsettings.Development.json`is not included in the
repository (`.gitignore`). `applicationId` and `token` are
not included in the `appsettings.Example.json` file and should not
be included in the `appsettings.Development.json` file. Instead use
the `dotnet user-secrets` command to set the secrets.