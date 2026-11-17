#!/bin/sh
# Installs the ygrab CLI of the latest release into ~/.local/bin, or into YGRAB_INSTALL_DIR
set -eu

releases=https://github.com/thomas-fazzari/ygrab/releases/latest/download

fail() {
  echo "$1" >&2
  exit 1
}

asset_name() {
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) echo ygrab-linux-x64 ;;
    Linux-aarch64 | Linux-arm64) echo ygrab-linux-arm64 ;;
    Darwin-arm64) echo ygrab-osx-arm64 ;;
    *) fail "No ygrab build exists for $(uname -s) on $(uname -m)." ;;
  esac
}

verify() {
  grep " $1\$" SHA256SUMS >expected || fail "SHA256SUMS has no line for $1."
  if command -v sha256sum >/dev/null; then
    sha256sum -c expected >/dev/null 2>&1 || fail "The checksum of $1 does not match SHA256SUMS."
  else
    shasum -a 256 -c expected >/dev/null 2>&1 || fail "The checksum of $1 does not match SHA256SUMS."
  fi
}

# the whole body runs from the last line, so that a truncated download runs nothing
main() {
  directory="${YGRAB_INSTALL_DIR:-$HOME/.local/bin}"
  asset="$(asset_name)"
  temporary="$(mktemp -d)"
  trap 'rm -rf "$temporary"' EXIT
  cd "$temporary"

  curl --fail --silent --show-error --location --remote-name "$releases/$asset"
  curl --fail --silent --show-error --location --remote-name "$releases/SHA256SUMS"
  verify "$asset"

  mkdir -p "$directory"
  install -m 755 "$asset" "$directory/ygrab"
  version="$("$directory/ygrab" --version)"
  echo "Installed ygrab $version into $directory."

  case ":$PATH:" in
    *":$directory:"*) ;;
    *) echo "Add this line to your shell profile to run ygrab: export PATH=\"$directory:\$PATH\"" ;;
  esac
}

main
