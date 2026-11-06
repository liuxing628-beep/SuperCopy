#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
version="$(tr -d '\r\n' < VERSION)"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION must be a three-part version." >&2; exit 1; }
# Work outside Documents so File Provider cannot add Finder metadata after signing.
build="$(mktemp -d "${TMPDIR:-/tmp}/supercopy-build.XXXXXX")"
trap 'rm -rf "$build"' EXIT
app="$build/SuperCopy.app"
archive="$root/dist/SuperCopy-macOS-v$version-English.zip"
mkdir -p "$root/dist" "$app/Contents/MacOS"
sources=(macos/Sources/*.swift)
for arch in arm64 x86_64; do
    xcrun --sdk macosx swiftc -swift-version 5 -O -target "$arch-apple-macosx13.0" \
        "${sources[@]}" -o "$build/SuperCopy-$arch"
done
xcrun lipo -create "$build/SuperCopy-arm64" "$build/SuperCopy-x86_64" -output "$app/Contents/MacOS/SuperCopy"
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>SuperCopy</string>
  <key>CFBundleIdentifier</key><string>io.github.liuxing628-beep.supercopy</string>
  <key>CFBundleName</key><string>SuperCopy</string>
  <key>CFBundleDisplayName</key><string>SuperCopy</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
plutil -lint "$app/Contents/Info.plist"
# File Provider and Finder can attach metadata that is not valid in a signed bundle.
xattr -cr "$app"
codesign --force --sign - "$app"
codesign --verify --strict --verbose=1 "$app"
architectures="$(xcrun lipo -archs "$app/Contents/MacOS/SuperCopy")"
for arch in arm64 x86_64; do
    [[ " $architectures " == *" $arch "* ]] || { echo "Missing $arch executable slice." >&2; exit 1; }
done
ditto -c -k --norsrc --keepParent "$app" "$build/package.zip"
mv -f "$build/package.zip" "$archive"
printf '%s\n' "$archive"
