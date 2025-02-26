FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

RUN mkdir -p /app

RUN apt update && apt install -y --fix-missing ffmpeg curl
RUN cp /usr/bin/ffmpeg /app/ffmpeg
RUN chmod +x /app/ffmpeg

RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o /app/yt-dlp
RUN chmod +x /app/yt-dlp

COPY src/DiscordMusic.sln src/DiscordMusic.sln
COPY src/DiscordMusic.Client/DiscordMusic.Client.csproj src/DiscordMusic.Client/DiscordMusic.Client.csproj
COPY src/DiscordMusic.Core/DiscordMusic.Core.csproj src/DiscordMusic.Core/DiscordMusic.Core.csproj
COPY src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Directory.Packages.props src/Directory.Packages.props
RUN dotnet restore src/DiscordMusic.sln

COPY . .

ARG TARGETARCH
RUN echo "Building for $TARGETARCH" && \
    case "$TARGETARCH" in \
        amd64)  RID="linux-x64";   LIB="linux-x86_64/libopus.so" ;; \
        arm)    RID="linux-arm";   LIB="linux-aarch64/libopus.so" ;; \
        arm64)  RID="win-x64"; LIB="win-x86_64/opus.dll" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "RID: $RID" && \
    dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -r "$RID" -o /app -v minimal --no-restore && \
    cp "/source/natives/$LIB" "/app/$(basename $LIB)"

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0.2-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["/app/dm"]
