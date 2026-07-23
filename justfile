set shell := ["fish", "-c"]

build target="linux/amd64":
    set arch (uname -m); \
    switch "$arch"; \
        case x86_64; set build_platform linux/amd64; \
        case aarch64 arm64; set build_platform linux/arm64; \
        case '*'; echo "Unsupported build architecture: $arch"; exit 1; \
    end; \
    buildah build --platform "{{target}}" --build-arg BUILDPLATFORM="$build_platform" --ignorefile .containerignore -t localhost/dm:test .

run:
    mkdir -p "{{justfile_directory()}}/artifacts/storage"
    podman rm -f dm || true
    podman run -d --name dm --env-file .env -v "{{justfile_directory()}}/artifacts/storage:/app/storage:Z" --platform linux/amd64 --restart unless-stopped localhost/dm:test

test: build run

action:
    act --workflows ".github/workflows/release.yml" --artifact-server-path artifacts/act

user-secrets-discord:
    #!/usr/bin/env fish
    read --silent --prompt-str "Discord Bot Token: " discord_token
    dotnet user-secrets set --project src/DiscordMusic.Client/DiscordMusic.Client.csproj "discord:token" "$discord_token"

user-secrets-spotify:
    #!/usr/bin/env fish
    read --silent --prompt-str "Spotify Client ID: " spotify_client_id
    read --silent --prompt-str "Spotify Client Secret: " spotify_client_secret
    dotnet user-secrets set --project src/DiscordMusic.Client/DiscordMusic.Client.csproj "spotify:clientId" "$spotify_client_id"
    dotnet user-secrets set --project src/DiscordMusic.Client/DiscordMusic.Client.csproj "spotify:clientSecret" "$spotify_client_secret"

manifest image="mcr.microsoft.com/dotnet/sdk" tag="10.0.301" arch="amd64" os="linux":
    podman manifest inspect {{image}}:{{tag}} \
      | jq -r '.manifests[] | select(.platform.architecture=="{{arch}}" and .platform.os=="{{os}}") | .digest'
