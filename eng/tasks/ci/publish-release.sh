#!/usr/bin/env bash
# Publishes the GitHub release of a tag with the CLI binaries of artifacts/release and their checksums.
# The notes come from git-cliff, followed by the image of the release
set -euo pipefail

tag="$1"
image="$2"
notes="$(mktemp)"

(cd artifacts/release && sha256sum ygrab-* >SHA256SUMS)
{
  git cliff --current --strip all
  printf '\nImage: %s\n' "\`$image:${tag#v}\`"
} >"$notes"
gh release create "$tag" artifacts/release/* --verify-tag --title "$tag" --notes-file "$notes"
