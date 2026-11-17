#!/usr/bin/env bash
# Publishes the native CLI for one runtime into artifacts/release, under the name of its release asset.
# With a tag such as v1.2.3, stamps that version and checks that the binary prints it
set -euo pipefail

rid="$1"
tag="${2:-}"

version=()
if [[ -n "$tag" ]]; then
  version=("-p:Version=${tag#v}" -p:IncludeSourceRevisionInInformationalVersion=false)
fi
dotnet publish src/YGrab.Cli --configuration Release --runtime "$rid" --output artifacts/cli "${version[@]}"

extension=""
if [[ "$rid" == win-* ]]; then
  extension=.exe
fi
binary="artifacts/release/ygrab-$rid$extension"
mkdir -p artifacts/release
cp "artifacts/cli/ygrab$extension" "$binary"

printed="$("$binary" --version)"
echo "$printed"
if [[ -n "$tag" && "$printed" != "${tag#v}" ]]; then
  echo "The binary prints $printed instead of ${tag#v}." >&2
  exit 1
fi
