# discord-music

Another music bot for Discord with playback controls, song lyrics and advanced queue management.

**Core Libraries & APIs**:

- [NetCord](https://github.com/NetCordDev/NetCord): For Discord interaction.
- [FFmpeg](https://github.com/FFmpeg/FFmpeg): For audio processing.
- [yt-dlp](https://github.com/yt-dlp/yt-dlp): For YouTube audio extraction.
- [SpotifyApi-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET): For Spotify integration.
- [Genius](https://genius.com): For lyrics fetching.

> **Important**: This bot uses `yt-dlp` to fetch YouTube audio streams. Since YouTube may block IP ranges from cloud
> providers, it's recommended to run the bot on a residential IP for reliable access. If you encounter the "confirm
> you're
> not a robot" error, your IP is likely blocked. A home network should work smoothly.

## Features

### 🎵 **Core Music Functions**

- **Join & Leave**: Auto-connect and disconnect from voice channels.
- **Play Music**:
  - Stream audio from YouTube (URLs or search queries).
  - Play from Spotify (search on Spotify and stream via YouTube).
- **Playback Control**: Pause, resume, and seek to specific timestamps.

### 📜 **Queue Management**

- **Queue System**:
  - Add, remove, clear, and skip tracks.

### 🎶 **Extra Features**

- **Lyrics Fetching**: Fetch lyrics for the currently playing song.
- **Audio Controls**: Interactive audio controls with buttons.
- **Audio Caching**: Cache tracks to reduce load times and enhance performance.
- **Auto Disconnect**: Automatically disconnect from the voice channel when empty.

### ⚙️ **Bot Management**

- **Docker Support**: Easily deploy the bot in a containerized environment.
- **Permission System**: Role-based access control for commands.

## Installation

> **Important**: Keep your tokens secret. If exposed, regenerate them immediately.

### Docker (Recommended)

To run the bot with Docker, use the following commands:

```bash
docker pull ghcr.io/bycrookie/discord-music:latest
docker run -d --restart always --platform linux/amd64 --env-file .env --name dm -v /var/tmp/dm/data:/data ghcr.io/bycrookie/discord-music:latest
```

Use the `--env-file` option to pass environment variables. Example `.env` files are available [here](.env.example).

For custom-builds, refer to the [Dockerfile](Dockerfile).

### Local Installation

**Supported Platforms**: `win-x64`, `linux-x64`, and `linux-arm64`. Other architectures may require additional
dependencies like `opus` and `libsodium`.

Make sure to change the cache location in the `.dmrc` file to a writable directory if you don't have write access to
`/data`.

#### Required Binaries and Libraries:

- **FFmpeg**: Use the static builds from the yt-dlp
  project: [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds/releases). Choose the archive matching your
  architecture (e.g. `ffmpeg-master-latest-linux64-gpl.tar.xz` or `ffmpeg-master-latest-linuxarm64-gpl.tar.xz`) and
  extract `ffmpeg` and `ffprobe`.
- **yt-dlp**: Install from [yt-dlp releases](https://github.com/yt-dlp/yt-dlp/releases) (or nightly builds if desired).
- Add them to your system PATH or place them in the bot's directory.
- **Opus**: Install the Opus codec if not available. Download from [Opus Codec](https://opus-codec.org/) or build from
  source.
- **Libsodium**: Install from [Libsodium](https://libsodium.org/) if needed or build from source.

## Configuration

> **Note**: Avoid storing sensitive information in `.dmrc`. Use environment variables instead.

The bot first looks for the `.dmrc` INI file in the executable directory. If it’s not found, it checks platform-specific
paths:

- **Linux/macOS**: `~/.dmrc`
- **Windows**: `%USERPROFILE%/.dmrc`

`DISCORD_MUSIC_CONFIG_FILE` environment variable can be used to specify a custom file path for the `.dmrc` file.

If a setting is missing from the `.dmrc` file, it will look for corresponding environment variables prefixed with
`DISCORD_MUSIC_`. For nested properties, use double underscores (`__`). Example:

```plaintext
DISCORD_MUSIC_DISCORD__TOKEN=your-token
DISCORD_MUSIC_DISCORD__ALLOW__0=music
```

An example `.dmrc` file is available [here](.dmrc.example).

## Support

If you enjoy the bot, consider supporting the project by starring the repository and contributing to its development
through the following methods:

<a href="https://buymeacoffee.com/bycrookie" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174"></a>

[:heart: Sponsor](https://github.com/sponsors/byCrookie)

## Development

For development, modify the [`.dmrc.ini`](src/DiscordMusic.Client/.dmrc.ini) file to test configuration changes. Keep
secrets secure by using tools
like [dotnet user-secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).
