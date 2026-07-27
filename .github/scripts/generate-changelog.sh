#!/usr/bin/env bash
set -euo pipefail

version="$1"
output_path="$2"

previous_tag="$(git describe --tags --abbrev=0 --match '[0-9]*' HEAD^ 2>/dev/null || true)"

if [[ -n "$previous_tag" ]]; then
    range="$previous_tag..HEAD"
    subtitle="_Changes since $previous_tag._"
else
    range="HEAD"
    subtitle="_Changes from git history._"
fi

mkdir -p "$(dirname "$output_path")"

section() {
    local title="$1"
    local types="$2"
    mapfile -t entries < <(git log --reverse --format=%s "$range" | sed -nE "s/^(${types})(\\([^)]*\\))?(!)?:[[:space:]]+(.+)$/- \\4/p")

    if (( ${#entries[@]} > 0 )); then
        printf '### %s\n\n' "$title"
        printf '%s\n' "${entries[@]}"
        printf '\n'
        generated=1
    fi
}

generated=0

{
    printf '## %s\n\n' "$version"
    printf '%s\n\n' "$subtitle"

    section "Features" "feat"
    section "Fixes" "fix"
    section "Performance" "perf"
    section "Refactoring" "refactor"
    section "Documentation" "docs"
    section "Tests" "test"
    section "Build and CI" "build|ci"
    section "Maintenance" "chore|style|revert"

    if (( generated == 0 )); then
        printf 'No Conventional Commit entries found.\n'
    fi
} > "$output_path"
