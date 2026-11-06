#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
mkdir -p macos/.build
xcrun --sdk macosx swiftc -swift-version 5 -O macos/Sources/Core.swift macos/Tests/CoreTests.swift -o macos/.build/CoreTests
macos/.build/CoreTests tests/fixtures/blocks.json
if [[ "${1:-}" == "--ui" ]]; then
    version="$(tr -d '\r\n' < VERSION)"
    unpacked="$(mktemp -d "${TMPDIR:-/tmp}/supercopy-test.XXXXXX")"
    trap 'rm -rf "$unpacked"' EXIT
    ditto -x -k "dist/SuperCopy-macOS-v$version-English.zip" "$unpacked"
    codesign --verify --strict "$unpacked/SuperCopy.app"
    "$unpacked/SuperCopy.app/Contents/MacOS/SuperCopy" --ui-smoke-test
fi
