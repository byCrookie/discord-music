ARG BUILDPLATFORM
ARG TARGETPLATFORM

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:493fca072aac81307027cbb7b7c9a82b6e87d222af315504d05dc6530e69b519 AS build
WORKDIR /build/libs

ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETARCH

# renovate: datasource=github-releases depName=yt-dlp/FFmpeg-Builds
ARG FFMPEG_VERSION=autobuild-2026-07-23-16-00
# renovate: datasource=github-releases depName=yt-dlp/yt-dlp-nightly-builds
ARG YTDLP_VERSION=2026.07.21.234255
# renovate: datasource=github-releases depName=denoland/deno
ARG DENO_VERSION=v2.9.4

RUN echo "Target platform: $TARGETPLATFORM | Build platform: $BUILDPLATFORM | Target architecture: $TARGETARCH" && \
    dotnet --version && dotnet --list-sdks && dotnet --info

RUN apt-get update && apt-get install -y --no-install-recommends curl xz-utils ca-certificates unzip && rm -rf /var/lib/apt/lists/*

RUN case "$TARGETARCH" in \
    amd64)  FFMPEG_ARCH="linux64" ;; \
    arm64)  FFMPEG_ARCH="linuxarm64" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    FFMPEG_BASE_URL="https://github.com/yt-dlp/FFmpeg-Builds/releases/download/${FFMPEG_VERSION}" && \
    FFMPEG_ASSET="$(curl -fsL "${FFMPEG_BASE_URL}/checksums.sha256" | awk -v arch="$FFMPEG_ARCH" '$2 ~ arch "-gpl\\.tar\\.xz$" { print $2; exit }')" && \
    test -n "$FFMPEG_ASSET" && \
    FFMPEG_URL="${FFMPEG_BASE_URL}/${FFMPEG_ASSET}" && \
    echo "Downloading FFmpeg from $FFMPEG_URL" && \
    curl -fL "$FFMPEG_URL" -o ffmpeg.tar.xz && \
    mkdir ffmpeg-extract && tar -xf ffmpeg.tar.xz -C ffmpeg-extract && rm ffmpeg.tar.xz && \
    cp ffmpeg-extract/*/bin/ffmpeg ffmpeg && cp ffmpeg-extract/*/bin/ffprobe ffprobe && \
    rm -rf ffmpeg-extract && \
    chmod +x ffmpeg ffprobe && \
    if [ "$TARGETPLATFORM" = "$BUILDPLATFORM" ]; then ./ffmpeg -version; else echo "Skipping FFmpeg smoke test for cross-build"; fi

RUN case "$TARGETARCH" in \
    amd64)  YTDLP_URL="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download/${YTDLP_VERSION}/yt-dlp_linux" ;; \
    arm64)  YTDLP_URL="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download/${YTDLP_VERSION}/yt-dlp_linux_aarch64" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading yt-dlp from $YTDLP_URL" && \
    curl -fL "$YTDLP_URL" -o yt-dlp && \
    chmod +x yt-dlp && \
    if [ "$TARGETPLATFORM" = "$BUILDPLATFORM" ]; then ./yt-dlp --version; else echo "Skipping yt-dlp smoke test for cross-build"; fi

RUN case "$TARGETARCH" in \
    amd64)  DENO_URL="https://github.com/denoland/deno/releases/download/${DENO_VERSION}/deno-x86_64-unknown-linux-gnu.zip" ;; \
    arm64)  DENO_URL="https://github.com/denoland/deno/releases/download/${DENO_VERSION}/deno-aarch64-unknown-linux-gnu.zip" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading Deno from $DENO_URL" && \
    curl -fL "$DENO_URL" -o deno.zip && \
    unzip -q deno.zip deno && \
    rm deno.zip && \
    chmod +x deno && \
    if [ "$TARGETPLATFORM" = "$BUILDPLATFORM" ]; then ./deno --version; else echo "Skipping Deno smoke test for cross-build"; fi

WORKDIR /build/source

COPY DiscordMusic.slnx DiscordMusic.slnx
COPY src/DiscordMusic.Client/DiscordMusic.Client.csproj src/DiscordMusic.Client/DiscordMusic.Client.csproj
COPY src/DiscordMusic.Core/DiscordMusic.Core.csproj src/DiscordMusic.Core/DiscordMusic.Core.csproj
COPY src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj
COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY global.json global.json

RUN case "$TARGETARCH" in \
    amd64)  RID="linux-x64" ;; \
    arm64)  RID="linux-arm64" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && echo "RID: $RID" && dotnet restore DiscordMusic.slnx -r "$RID" -v minimal

COPY . .

RUN case "$TARGETARCH" in \
    amd64)  RID="linux-x64" ;; \
    arm64)  RID="linux-arm64" ;; \
    *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && echo "RID: $RID" && \
    dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -c Release -r "$RID" -o /build/publish --no-restore -v minimal

COPY containers/entrypoint.sh /build/publish/entrypoint.sh
RUN chmod +x /build/publish/entrypoint.sh

FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe AS final
WORKDIR /app

RUN apt-get update && apt-get install -y \
    libstdc++6 libgomp1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /build/publish .
COPY --from=build \
    /build/libs/ffmpeg \
    /build/libs/ffprobe \
    /build/libs/yt-dlp \
    /build/libs/deno \
    /usr/bin/

ENV DOTNET_EnableDiagnostics=0
ENV DISCORD_MUSIC_STORAGE__PATH="/app/storage"

ENTRYPOINT ["/app/entrypoint.sh"]
