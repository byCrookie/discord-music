FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build/libs

ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETARCH

RUN echo "Target platform: $TARGETPLATFORM"
RUN echo "Build platform: $BUILDPLATFORM"
RUN echo "Target architecture: $TARGETARCH"

RUN apt update && apt install -y --fix-missing curl xz-utils

RUN case "$TARGETARCH" in \
        amd64)  FFMPEG_URL="https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-amd64-static.tar.xz" ;; \
        arm64)  FFMPEG_URL="https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-arm64-static.tar.xz" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading FFmpeg from $FFMPEG_URL" && \
    curl -L "$FFMPEG_URL" -o ffmpeg.tar.xz && \
    tar -xf ffmpeg.tar.xz --strip-components=1 && \
    chmod +x ffmpeg

RUN case "$TARGETARCH" in \
        amd64)  YTDLP_URL="https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux" ;; \
        arm64)  YTDLP_URL="https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux_arm64" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "Downloading yt-dlp from $YTDLP_URL" && \
    curl -L "$YTDLP_URL" -o yt-dlp && \
    chmod +x yt-dlp

WORKDIR /build/source

COPY src/DiscordMusic.sln src/DiscordMusic.sln
COPY src/DiscordMusic.Client/DiscordMusic.Client.csproj src/DiscordMusic.Client/DiscordMusic.Client.csproj
COPY src/DiscordMusic.Core/DiscordMusic.Core.csproj src/DiscordMusic.Core/DiscordMusic.Core.csproj
COPY src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Directory.Packages.props src/Directory.Packages.props
RUN dotnet restore src/DiscordMusic.sln

COPY . .

RUN case "$TARGETARCH" in \
        amd64)  RID="linux-x64";   LIB="linux-x86_64/libopus.so" ;; \
        arm64)  RID="linux-arm64";   LIB="linux-aarch64/libopus.so" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "RID: $RID" && \
    dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -r "$RID" -o /build/publish -v minimal --no-restore && \
    cp "natives/$LIB" "/build/publish/$(basename $LIB)"

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0.2-noble-chiseled-extra AS final
WORKDIR /app

COPY --from=build /build/publish .
COPY --from=build /build/libs/ffmpeg /usr/bin/ffmpeg
COPY --from=build /build/libs/yt-dlp /usr/bin/yt-dlp

ENTRYPOINT ["/app/dm"]
