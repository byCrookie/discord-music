#!/usr/bin/env bash
set -euo pipefail

if [[ "${GITHUB_EVENT_NAME}" == "pull_request" ]]; then
    range="${BASE_SHA}..HEAD"
else
    if [[ "${BEFORE_SHA}" =~ ^0+$ ]]; then
        if git rev-parse "${AFTER_SHA}^" >/dev/null 2>&1; then
            range="${AFTER_SHA}^..${AFTER_SHA}"
        else
            range="${AFTER_SHA}"
        fi
    else
        range="${BEFORE_SHA}..${AFTER_SHA}"
    fi
fi

regex='^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([A-Za-z0-9._/-]+\))?!?: .+'
failed=0

while IFS= read -r subject; do
    [[ -z "$subject" ]] && continue
    [[ "$subject" == Merge\ * ]] && continue

    if [[ ! "$subject" =~ $regex ]]; then
        echo "::error::Invalid Conventional Commit subject: $subject"
        failed=1
    fi
done < <(git log --format=%s "$range")

exit "$failed"
