FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
ARG RUNTIME=linux-x64
COPY . .
RUN dotnet publish ./DiscordMusic.Cli/DiscordMusic.Cli.csproj --runtime ${RUNTIME} --output /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
ARG VERBOSITY=information
COPY --from=build /app .

RUN rm appsettings.json
RUN apt-get update && apt-get install -y ffmpeg --fix-missing
RUN wget https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -O yt-dlp
RUN chmod +x yt-dlp

RUN echo "#!/bin/sh" > entrypoint.sh
RUN echo "./dm run --verbosity $VERBOSITY" >> entrypoint.sh
RUN chmod +x entrypoint.sh

USER $APP_UID

ENTRYPOINT ["./entrypoint.sh"]