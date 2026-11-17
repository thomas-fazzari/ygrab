#!/usr/bin/env bash
# Commits the notes of the next version into web/src/changelog.json, then tags that commit
set -euo pipefail

changelog=web/src/changelog.json

# Keeps the releases with a change and the fields that the release notes page reads.
# The author is the GitHub login of a noreply address, or else the git name
filter='
map(select(.commits | length > 0) | {
  version: (.version | ltrimstr("v")),
  date: (.timestamp | strftime("%Y-%m-%d")),
  changes: [.commits[] | {
    type: .group,
    text: .message,
    author: ((.author.email | capture("^([0-9]+\\+)?(?<login>[^@]+)@users\\.noreply\\.github\\.com$").login) // .author.name),
    sha: .id
  }]
})'

if [[ "$(git branch --show-current)" != master ]]; then
  echo "Release from master. A squash merge would drop the tagged commit." >&2
  exit 1
fi

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Commit or stash the changes first. The release commit holds only the changelog." >&2
  exit 1
fi

version="$(git cliff --bumped-version)"
notes="$(git cliff --tag "$version" --context | jq "$filter")"
printf '%s\n' "$notes" >"$changelog"
bun x --cwd web oxfmt --write "${changelog#web/}"
git commit --message "chore: release $version" -- "$changelog"
git tag --annotate "$version" --message "$version"
