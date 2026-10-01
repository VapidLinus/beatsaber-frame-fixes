#!/usr/bin/env bash
# Downloads the beatsaber-frame-fixes patcher for this device from the latest GitHub release, verifies its
# checksum and runs it. All arguments are passed to the patcher; run with --help to see them.
#
#   curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash
#
# BSFF_BINARY=/path/to/patcher skips the download and runs that patcher instead.
set -euo pipefail

repo="VapidLinus/beatsaber-frame-fixes"

case "$(uname -m)" in
    aarch64 | arm64) asset="beatsaber-frame-fixes-linux-arm64" ;;
    x86_64 | amd64) asset="beatsaber-frame-fixes-linux-x64" ;;
    *)
        echo "Sorry, this device ($(uname -m)) isn't supported." >&2
        exit 1
        ;;
esac

if [[ -n "${BSFF_BINARY:-}" ]]; then
    exec "$BSFF_BINARY" "$@"
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Downloading the patcher..."
base="https://github.com/$repo/releases/latest/download"
if ! curl -fsSL -o "$work/$asset" "$base/$asset" || ! curl -fsSL -o "$work/SHA256SUMS" "$base/SHA256SUMS"; then
    echo "The download failed. Check your internet connection and try again." >&2
    exit 1
fi

if ! (cd "$work" && grep " $asset\$" SHA256SUMS | sha256sum --check --status); then
    echo "The downloaded patcher is damaged (checksum mismatch). Try again." >&2
    exit 1
fi

chmod +x "$work/$asset"
"$work/$asset" "$@"
