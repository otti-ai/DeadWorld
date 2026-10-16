#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || "$1" != v* || "$1" == *[!A-Za-z0-9._-]* ]]; then
    echo "Usage: $0 v<version> (example: $0 v0.0.1)" >&2
    exit 2
fi

command -v zip >/dev/null 2>&1 || {
    echo "The zip command is required to package the release." >&2
    exit 1
}

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for required_path in About/About.xml Defs Languages; do
    if [[ ! -e "$repo_dir/$required_path" ]]; then
        echo "Required mod content is missing: $required_path" >&2
        exit 1
    fi
done

if [[ ! -f "$repo_dir/Assemblies/DeadWorld.dll" ]]; then
    echo "Build the mod first; Assemblies/DeadWorld.dll is missing." >&2
    exit 1
fi

version="$1"
output_dir="${TMPDIR:-/tmp}/deadworld-release"
archive="$output_dir/DeadWorld-$version.zip"
if [[ -e "$archive" ]]; then
    echo "Release archive already exists: $archive" >&2
    echo "Move or remove it before packaging this version again." >&2
    exit 1
fi

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

mkdir -p "$output_dir" "$work_dir/DeadWorld/Assemblies"
cp -a "$repo_dir/About" "$repo_dir/Defs" "$repo_dir/Languages" "$work_dir/DeadWorld/"
for assembly in "$repo_dir"/Assemblies/*.dll; do
    [[ -f "$assembly" ]] && cp "$assembly" "$work_dir/DeadWorld/Assemblies/"
done

(cd "$work_dir" && zip -qr "$archive" DeadWorld)
printf 'Created %s\n' "$archive"