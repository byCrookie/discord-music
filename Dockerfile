FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /source
COPY . .

ARG TARGETARCH
RUN echo "Building for $TARGETARCH" && \
    case "$TARGETARCH" in \
        amd64)  RID="linux-x64";   LIB="linux-x86_64/libopus.so" ;; \
        arm)    RID="linux-arm";   LIB="linux-aarch64/libopus.so" ;; \
        arm64)  RID="linux-arm64"; LIB="win-x86_64/opus.dll" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "RID: $RID" && \
    dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -r "$RID" --self-contained true -o /app -v minimal && \
    cp "/source/natives/$LIB" "/app/$(basename $LIB)"

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

RUN apt update && apt install -y --fix-missing ffmpeg curl && \
    curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o yt-dlp && \
    chmod +x yt-dlp && \
    cp /usr/bin/ffmpeg /app/ffmpeg 

RUN adduser -u 1000 user
USER user

ENTRYPOINT ["/app/dm"]
