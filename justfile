set shell := ["pwsh", "-c"]

build:
    podman build -t dm:test .

run:
    podman run -d --name dm --env-file .env -v "${PWD}/data:/data" --platform linux/amd64 --restart unless-stopped dm:test
    
stable:
    skopeo copy --preserve-digests --dest-creds {{username}}:{{password}} --all docker://ghcr.io/bycrookie/discord-music:{{stable-version}} docker://ghcr.io/bycrookie/discord-music:stable

test: build run
