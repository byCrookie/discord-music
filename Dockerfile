FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
COPY . /source
WORKDIR /source

ARG TARGETARCH
RUN echo "Building for $TARGETARCH" && \
    case "$TARGETARCH" in \
        amd64)  RID="linux-x64";   LIB="linux-x86_64/libopus.so"; OUT="libopus.so" ;; \
        arm)    RID="linux-arm";   LIB="linux-aarch64/libopus.so"; OUT="libopus.so" ;; \
        arm64)  RID="linux-arm64"; LIB="win-x86_64/opus.dll"; OUT="opus.dll" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    echo "RID: $RID" && \
    dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -r "$RID" -o /app -v minimal && \
    cp "/source/natives/$LIB" "/app/$OUT"

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

RUN apt update && apt install -y ffmpeg --fix-missing && apt install -y curl --fix-missing
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o yt-dlp
RUN chmod +x yt-dlp
RUN cp /usr/bin/ffmpeg /app/ffmpeg

RUN adduser -u 1000 user
USER user

ENTRYPOINT ["/app/dm"]
