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

## Installation

> Warning: Tokens should be kept secret and not shared with anyone. If tokens are shared or exposed, they should be
> regenerated.

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

## Local

> Runtimes: Only win-x64, linux-x64 and linux-arm64 are currently fully supported. The bot will not work on other
> architectures if opus and libsodium are not installed on the system. It is recommended to use the docker image for
> to run the bot.

> Note: If you do not want to use the `.dmrc` file, you can use environment variables prefixed
> with `DISCORD_MUSIC_`. This is required when using docker to deploy discord-music. Further information can be found in
> the [Configuration](#Configuration) section.

### Required Binaries and Libraries

The bot requires FFmpeg and yt-dlp to be installed on the system. Download it
from https://www.ffmpeg.org/download.html and https://github.com/yt-dlp/yt-dlp/releases.
Make the binaries discoverable by adding them to the system path or place them in the same directory as the bot.
If you want to specify paths explicitly, change the `ffmpeg` and `ytdlp` value in the `.dmrc` file to the path of
the executable.

#### Opus

The bot requires the Opus codec to be installed on the system. Some platforms/runtimes are directly supported
by discord-music and do not require the Opus codec to be installed. If you receive an error message about the Opus
codec not being found, find it under [Natives](natives) or download it from https://opus-codec.org/ if possible. If
downloading the codec is not possible, build it from source or try to find a pre-built version of the dll for your
platform.

#### Libsodium

The bot requires the Libsodium library to be installed on the system. Some platforms/runtimes are directly supported
by discord-music and do not require the Libsodium library to be installed. If you receive an error message about the
Libsodium library not being found, download it from https://libsodium.org/ if possible or build it from
source.

## Configuration

The bot uses the `.dmrc` ini-file for configuration values. If a value is not found in the `.dmrc`
file it will look for an environment variable prefixed with `DISCORD_MUSIC_`.
Make sure to use double underscores `__` for nested properties. Example: `DISCORD_MUSIC_DISCORD__TOKEN`.
When providing a list, use an indexer `__0` for the first item, `__1` for the second item and so on.
Example: `DISCORD_MUSIC_DISCORD__ALLOW__0=music`. An example `.dmrc` file can be found [here](.dmrc.example).

## Development

During development change the [`.dmrc.ini`](src/DiscordMusic.Client/.dmrc.ini) file to test settings.
Secrets should be kept secret [`dotnet user-secrets`](#Secrets). Use the [
`dotnet user-secrets`](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).
