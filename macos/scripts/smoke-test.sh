#!/bin/bash
# End-to-end check of the built app: opens the real wheel, steers onto the slice of Calculator and
# TextEdit, releases, and verifies macOS reports that app as frontmost (see App/SelfTest.swift).
# No permission is needed. Usage: scripts/smoke-test.sh [path/to/Whirl.app]
set -uo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="${1:-$DIR/build/Whirl.app}"
BIN="$APP/Contents/MacOS/Whirl"

echo "Listing"
"$BIN" --list | head -5
[ "${PIPESTATUS[0]}" = 0 ] || { echo "  ✗ --list failed"; exit 1; }

echo "Wheel"
"$BIN" --self-test
