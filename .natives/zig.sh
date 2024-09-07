#!/bin/bash

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <target_os> <target_arch>"
  exit 1
fi

TARGET_OS=$1
TARGET_ARCH=$2

JSON_URL="https://ziglang.org/download/index.json"
DOWNLOAD_URL=$(curl -s $JSON_URL | jq -r ".[\"0.13.0\"][\"$TARGET_ARCH-$TARGET_OS\"].tarball")

if [ -z "$DOWNLOAD_URL" ] || [ "$DOWNLOAD_URL" == "null" ]; then
  echo "No download URL found for OS: $TARGET_OS"
  exit 1
fi

echo "Downloading $DOWNLOAD_URL..."
curl -O "$DOWNLOAD_URL"

if [ $? -eq 0 ]; then
  echo "Download successful."
else
  echo "Download failed."
  exit 1
fi

FILENAME=$(basename "$DOWNLOAD_URL")
echo "Extracting $FILENAME..."
tar -xJf $FILENAME

if [ $? -ne 0 ]; then
  echo "Extraction failed."
  exit 1
fi

BASENAME="${FILENAME%.*.*}"
echo "Renaming $BASENAME to zig..."
mv $BASENAME zig

echo "Extraction complete."