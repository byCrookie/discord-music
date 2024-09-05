FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish ./DiscordMusic.Cli/DiscordMusic.Cli.csproj --use-current-runtime --output /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
COPY --from=build /app .

RUN rm appsettings.json
RUN apt-get update && apt-get install -y ffmpeg --fix-missing
RUN wget https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -O yt-dlp
RUN chmod +x yt-dlp

USER $APP_UID

ENTRYPOINT ["/app/dm", "run", "--verbosity", "trace"]
