# discord-music

Another music bot for discord. This bot is written in C# and
uses [NetCord](https://github.com/NetCordDev/NetCord), [FFmpeg](https://github.com/FFmpeg/FFmpeg), [yt-dlp](https://github.com/yt-dlp/yt-dlp),
[SpotifyApi-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET)
and [Genius](https://genius.com) for lyrics.

> **Important:** This bot uses yt-dlp to fetch YouTube audio streams. Since YouTube often blocks IP ranges from cloud
> providers, it's recommended to use a residential IP for reliable access. If you encounter a "confirm you're not a
> robot" error, your IP is likely blocked. Running the bot from a home network should work smoothly.

## Features

#### 🎵 **Core Music Functions**

- **Join & Leave**: Connects to and disconnects from voice channels.
- **Play Music**: Streams audio from various sources:
    - **YouTube**: Play music via URLs or search queries.
    - **Spotify**: Search on spotify, play from YouTube.
- **Pause, Resume & Seek**: Control playback, skip to specific timestamps or seek.

#### 📜 **Queue Management**

- **Queue System**:
    - Add, remove, clear tracks.
    - Skip individual songs.

#### 🎶 **Extra Features**

- **Lyrics Fetching**: Retrieve lyrics for the currently playing song.
- **Audio Controls**: Controls audio using interactive buttons.
- **Audio Caching**: Tracks are cached to reduce loading times.
- **Auto Disconnect**: Leaves the voice channel when empty.

#### ⚙️ **Bot Management**

- **Docker Support**: Run in a containerized environment.
- **Permission System**:
    - Simple allow/deny command system.
    - Role-based access control for commands.

## Docker

> Recommended: Use the docker image to run the bot.

Pre-built docker images can be found
on [GitHub - Discord Music](https://github.com/byCrookie/discord-music/pkgs/container/discord-music). There are
pre-built images for `linux/amd64` and `linux/arm64`.

All config values have to be provided as environment variables.
This can be achieved by using the `--env-file` option of the `docker run` command or other methods to pass environment
variables to the container. An example `.env` can be found [here](.env.example). The cache location should be mounted as
a volume to persist the cache between container
restarts.

```sh
docker pull ghcr.io/bycrookie/discord-music:latest
docker run -d --restart always --platform linux/amd64 --env-file .env --name dm -v /var/tmp/dm/data:/data ghcr.io/bycrookie/discord-music:latest
```

[Dockerfile](Dockerfile) lets you build your own docker image of the bot, it is recommended to use the pre-built images.

## Runtimes

> Warning: Only win-x64, linux-x64 and linux-arm64 are currently fully supported. The bot will not work on other
> architectures if opus and libsodium are not installed on the system. It is recommended to use the docker image for
> to run the bot.

## Setup

> Warning: The token should be kept secret and not shared with anyone. If the token is shared, it should be regenerated.

> Note: If you do not want to use the `appsettings.json` file, you can use environment variables prefixed
> with `DISCORD_MUSIC_`. This is required when using docker to deploy discord-music. Further information can be found in
> the [Configuration](#Configuration) section.

### Configuration

The bot uses the `appsettings.json` file for configuration values. If a value is not found in the `appsettings.json`
file it will look for an environment variable prefixed with `DISCORD_MUSIC_`.
Make sure to use double underscores `__` for nested properties. Example: `DISCORD_MUSIC_DISCORD__TOKEN`.
When providing a list, use an indexer `__0` for the first item, `__1` for the second item and so on.
Example: `DISCORD_MUSIC_DISCORD__ALLOW__0=music`.

### Discord

Go to https://discord.com/developers/applications and create a new application.

Replace the `Discord:ApplicationId` in the `appsettings.json` file with the application id of your new application.
Next replace the `Discord:Token` in the `appsettings.json` file with the token of your new application.
Environment variables can be used to set the token and application id.

- `DISCORD_MUSIC_DISCORD__TOKEN`
- `DISCORD_MUSIC_DISCORD__APPLICATIONID`

### Spotify (Optional)

Go to https://developer.spotify.com/dashboard/applications and create a new application.

Replace the `Spotify:ClientId` in the `appsettings.json` file with the client
id of your new application. Next replace the `Spotify:ClientSecret` in the `appsettings.json`
file with the client secret of your new application.
Environment variables can be used to set the client id and client secret.

- `DISCORD_MUSIC_SPOTIFY__CLIENTID`
- `DISCORD_MUSIC_SPOTIFY__CLIENTSECRET`

### Genius (Optional)

Go to https://genius.com/api-clients and create a new application.

Replace the `Lyrics:Token` in the `appsettings.json` file with the token of your new application.
Environment variables can be used to set the token.

- `DISCORD_MUSIC_LYRICS__TOKEN`

### FFmpeg

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

### yt-dlp

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

## Development

### Opus

The bot requires the Opus codec to be installed on the system. Some platforms/runtimes are directly supported
by discord-music and do not require the Opus codec to be installed. If you receive an error message about the Opus
codec not being found, find it under [Natives](natives) or download it from https://opus-codec.org/ if possible. If
downloading the codec is not possible, build it from source or try to find a pre-built version of the dll for your
platform.

### Libsodium

The bot requires the Libsodium library to be installed on the system. Some platforms/runtimes are directly supported
by discord-music and do not require the Libsodium library to be installed. If you receive an error message about the
Libsodium library not being found, download it from https://libsodium.org/ if possible or build it from
source.

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

During development use the `appsettings.Development.json` file to store settings.
Secrets should not be included in the `appsettings.Development.json` file,
instead use the `dotnet user-secrets` command to set the secrets.

### Docker Arm64

> Warning: At the moment building on windows for linux-arm64 does not work. Use a linux machine to build the docker
> image for linux-arm64.

To build docker image for linux-arm64 on windows use the `binfmt` image to register the arm64 architecture.

```sh
docker run --rm --privileged multiarch/qemu-user-static --reset -p yes
docker run --privileged --rm tonistiigi/binfmt --install all
docker run --rm --platform linux/arm64 alpine uname -m
docker buildx create --name mybuilder --driver docker-container --use
docker buildx inspect --bootstrap
docker buildx build --platform linux/arm64 -t dm:latest . --load
docker run -d --platform linux/arm64 --env-file .env --name dm dm:latest
```
