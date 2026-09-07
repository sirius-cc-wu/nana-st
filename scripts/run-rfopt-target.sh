#!/usr/bin/env bash
set -euo pipefail

repository_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"

if [[ $(uname -s) != Linux || $(uname -m) != x86_64 ]]; then
    printf '%s\n' 'rfopt target gate requires an AMD64 Linux host' >&2
    exit 1
fi

if [[ ! -e rfopt/.git ]]; then
    printf '%s\n' 'rfopt submodule is not initialized' >&2
    exit 1
fi

if [[ -n $(git -C rfopt status --porcelain --untracked-files=all) ]]; then
    printf '%s\n' 'rfopt submodule must be clean before running the target gate' >&2
    exit 1
fi

expected_commit=$(git rev-parse HEAD:rfopt)
actual_commit=$(git -C rfopt rev-parse HEAD)
if [[ $actual_commit != "$expected_commit" ]]; then
    printf 'rfopt submodule HEAD %s does not match NanaST gitlink %s\n' \
        "$actual_commit" "$expected_commit" >&2
    exit 1
fi

exec cargo test --locked --test rfopt_target
