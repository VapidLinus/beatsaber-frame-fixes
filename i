#!/usr/bin/env bash
# Short address for the beatsaber-frame-fixes installer:
#   curl -L vapidlinus.github.io/beatsaber-frame-fixes/i | bash
# Options go after "bash -s --", for example: ... | bash -s -- --restore
# Runs beatsaber-frame-fixes.sh from the main branch, which downloads the patcher from the latest release.
set -euo pipefail
curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash -s -- "$@"
