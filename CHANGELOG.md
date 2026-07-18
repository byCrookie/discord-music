**v5.0.0 changes:**

- Reworked playback around a queued track system with new voice connection handling.
- Added persistent track storage, storage size/clear commands, and automatic storage trimming.
- Switched configuration to `.env`/environment variables only; removed `.dmrc` config files.
- Updated YouTube search/download handling with background processing and ffmpeg PCM conversion.
- Refreshed Discord music commands and audio-bar controls, including stop, queue, play-next, and seek.
- Updated container setup, solution layout, and documentation for the new version.
- Removed the old audio stream, cache, and voice-command implementation.
