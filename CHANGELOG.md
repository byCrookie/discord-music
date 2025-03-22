**Changes:**

- Keep yt-dlp up to date by periodically updating `yt-dlp -U` inside running container
- Move to INI configuration for better readability and maintainability with linux style .dmrc file
- Allow .dmrc to be place anywhere using environment variable `DISCORD_MUSIC_CONFIG_FILE`