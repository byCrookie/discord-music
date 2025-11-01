FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build/libs

ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETARCH

RUN echo "Target platform: $TARGETPLATFORM | Build platform: $BUILDPLATFORM | Target architecture: $TARGETARCH" && \
    dotnet --version && dotnet --list-sdks && dotnet --info

RUN apt-get update && apt-get install -y --no-install-recommends curl xz-utils ca-certificates unzip && rm -rf /var/lib/apt/lists/*

RUN case "$TARGETARCH" in \
    amd64)  FFMPEG_URL="https://github.com/yt-dlp/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-linux64-gpl.tar.xz" ;; \
    arm64)  FFMPEG_URL="https://github.com/yt-dlp/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-linuxarm64-gpl.tar.xz" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading FFmpeg from $FFMPEG_URL" && \
    curl -L "$FFMPEG_URL" -o ffmpeg.tar.xz && \
    mkdir ffmpeg-extract && tar -xf ffmpeg.tar.xz -C ffmpeg-extract && rm ffmpeg.tar.xz && \
    cp ffmpeg-extract/*/bin/ffmpeg ffmpeg && cp ffmpeg-extract/*/bin/ffprobe ffprobe && \
    rm -rf ffmpeg-extract && \
    chmod +x ffmpeg ffprobe && ./ffmpeg -version

RUN case "$TARGETARCH" in \
    amd64)  YTDLP_URL="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp_linux" ;; \
    arm64)  YTDLP_URL="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp_linux_aarch64" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading yt-dlp from $YTDLP_URL" && \
    curl -L "$YTDLP_URL" -o yt-dlp && \
    chmod +x yt-dlp && ./yt-dlp --version

RUN case "$TARGETARCH" in \
    amd64)  DENO_URL="https://github.com/denoland/deno/releases/latest/download/deno-x86_64-unknown-linux-gnu.zip" ;; \
    arm64)  DENO_URL="https://github.com/denoland/deno/releases/latest/download/deno-aarch64-unknown-linux-gnu.zip" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading Deno from $DENO_URL" && \
    curl -L "$DENO_URL" -o deno.zip && \
    unzip -q deno.zip deno && \
    rm deno.zip && \
    chmod +x deno && ./deno --version

WORKDIR /build/source

COPY src/DiscordMusic.slnx src/DiscordMusic.slnx
COPY src/DiscordMusic.Client/DiscordMusic.Client.csproj src/DiscordMusic.Client/DiscordMusic.Client.csproj
COPY src/DiscordMusic.Core/DiscordMusic.Core.csproj src/DiscordMusic.Core/DiscordMusic.Core.csproj
COPY src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Directory.Packages.props src/Directory.Packages.props
COPY src/global.json src/global.json

RUN case "$TARGETARCH" in \
    amd64)  RID="linux-x64" ;; \
    arm64)  RID="linux-arm64" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && echo "RID: $RID" && dotnet restore src/DiscordMusic.slnx -r "$RID" -v minimal

COPY . .

RUN case "$TARGETARCH" in \
    amd64)  RID="linux-x64";   LIB="linux-x86_64/libopus.so" ;; \
    arm64)  RID="linux-arm64"; LIB="linux-aarch64/libopus.so" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && echo "RID: $RID" && \
    dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -c Release -r "$RID" -o /build/publish --no-restore -v minimal && \
    cp "natives/$LIB" "/build/publish/$(basename $LIB)"

RUN printf '#!/usr/bin/env sh\nset -e\n( while true; do /usr/bin/yt-dlp -U || true; sleep 86400; done ) &\nexec /app/dm "$@"\n' > /build/publish/entrypoint.sh && chmod +x /build/publish/entrypoint.sh

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

COPY --from=build /build/publish .
COPY --from=build /build/libs/ffmpeg /usr/bin/ffmpeg
COPY --from=build /build/libs/ffprobe /usr/bin/ffprobe
COPY --from=build /build/libs/yt-dlp /usr/bin/yt-dlp
COPY --from=build /build/libs/deno /usr/bin/deno

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["/app/entrypoint.sh"]
