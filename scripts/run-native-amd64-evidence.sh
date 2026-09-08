#!/usr/bin/env bash
set -euo pipefail

repository_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"

if [[ $(uname -s) != Linux || $(uname -m) != x86_64 ]]; then
    printf '%s\n' 'native AMD64 evidence requires an x86_64 Linux host' >&2
    exit 1
fi

printf 'Running rfopt unit and integration tests on native AMD64 Linux (%s)\n' "$(uname -r)"
printf 'rfopt revision: %s\n' "$(git -C rfopt rev-parse --verify HEAD)"

cargo test --locked --manifest-path rfopt/Cargo.toml
scripts/run-rfopt-target.sh
