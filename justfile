set shell := ["pwsh", "-c"]

build:
    podman build -t dm:latest .

run:
    podman run -d --name dm --env-file .env -v "${PWD}/data:/data" --platform linux/amd64 --restart always dm:latest

test: build run
