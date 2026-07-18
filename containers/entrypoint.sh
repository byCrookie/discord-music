#!/usr/bin/env sh
set -e

if [ "${DISCORD_MUSIC_YTDLP_AUTO_UPDATE:-true}" = "true" ]; then
    (
        while true; do
            /usr/bin/yt-dlp -U || true
            sleep 86400
        done
    ) &
fi

exec /app/dm "$@"
