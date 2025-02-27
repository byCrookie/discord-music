FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build/libs

RUN apt update && apt install -y --fix-missing curl xz-utils
RUN curl -L https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-amd64-static.tar.xz -o ffmpeg.tar.xz
RUN tar -xf ffmpeg.tar.xz --strip-components=1
RUN chmod +x ffmpeg

RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o yt-dlp
RUN chmod +x yt-dlp

WORKDIR /build/source

COPY src/DiscordMusic.sln src/DiscordMusic.sln
COPY src/DiscordMusic.Client/DiscordMusic.Client.csproj src/DiscordMusic.Client/DiscordMusic.Client.csproj
COPY src/DiscordMusic.Core/DiscordMusic.Core.csproj src/DiscordMusic.Core/DiscordMusic.Core.csproj
COPY src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj src/DiscordMusic.Core.Tests/DiscordMusic.Core.Tests.csproj
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Directory.Packages.props src/Directory.Packages.props
RUN dotnet restore src/DiscordMusic.sln

COPY . .

RUN dotnet publish src/DiscordMusic.Client/DiscordMusic.Client.csproj -r linux-x64 -o /build/publish -v minimal --no-restore
RUN cp "natives/linux-x86_64/libopus.so" "/build/publish/libopus.so"

FROM mcr.microsoft.com/dotnet/aspnet:9.0.2-noble-chiseled-extra-amd64 AS final
WORKDIR /app
COPY --from=build /build/publish .
COPY --from=build /build/libs/ffmpeg /usr/bin/ffmpeg
COPY --from=build /build/libs/yt-dlp /usr/bin/yt-dlp

ENTRYPOINT ["/app/dm"]
