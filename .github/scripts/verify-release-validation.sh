#!/usr/bin/env bash
set -euo pipefail

state="success"
description="Release validation passed."
failures=()

if [[ "${BUILD_RESULT:-}" != "success" ]]; then
    echo "::error::Release build failed."
    failures+=("build")
fi

if [[ "${CHANGELOG_RESULT:-}" != "success" ]]; then
    echo "::error::Release changelog generation failed."
    failures+=("changelog")
fi

if [[ "${CONTAINER_RESULT:-}" != "success" && "${CONTAINER_RESULT:-}" != "skipped" ]]; then
    echo "::error::Release container validation failed."
    failures+=("container")
fi

if ((${#failures[@]} > 0)); then
    state="failure"
    description="Failed: $(IFS=', '; echo "${failures[*]}")"
fi

gh api \
    --method POST \
    "repos/${GITHUB_REPOSITORY}/statuses/${GITHUB_SHA}" \
    --field state="$state" \
    --field context="Release validation" \
    --field description="$description" \
    --field target_url="${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}"

if [[ "$state" != "success" ]]; then
    exit 1
fi
