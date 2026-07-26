#!/usr/bin/env bash
# Post-publish script: package VisualStates as a macOS .app bundle.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_NAME="VisualStates"
EXECUTABLE_NAME="VisualStates"
BUNDLE_ID="com.aruantec.visualstates"
MIN_SYS_VER="12.3"
APP_VERSION="${APP_VERSION:-1.0.0}"
APP_VERSION="${APP_VERSION#v}"

PUBLISH_DIR="${1:-$(pwd)}"
PUBLISH_DIR="$(cd "$PUBLISH_DIR" && pwd)"
BUNDLE_DIR="$PUBLISH_DIR/$APP_NAME.app"

echo "Creating bundle at $BUNDLE_DIR"
rm -rf "$BUNDLE_DIR"
mkdir -p "$BUNDLE_DIR/Contents/MacOS" "$BUNDLE_DIR/Contents/Resources"

find "$PUBLISH_DIR" -mindepth 1 -maxdepth 1 ! -name "$APP_NAME.app" \
  -exec cp -R {} "$BUNDLE_DIR/Contents/MacOS/" \;

rm -rf "$BUNDLE_DIR/Contents/MacOS/packaging" "$BUNDLE_DIR/Contents/MacOS/Linux"

if [[ ! -f "$BUNDLE_DIR/Contents/MacOS/$EXECUTABLE_NAME" ]]; then
  echo "error: published executable not found in $PUBLISH_DIR" >&2
  exit 1
fi
chmod +x "$BUNDLE_DIR/Contents/MacOS/$EXECUTABLE_NAME"

cat > "$BUNDLE_DIR/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple Computer//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_ID</string>
  <key>CFBundleExecutable</key>
  <string>$EXECUTABLE_NAME</string>
  <key>CFBundleVersion</key>
  <string>$APP_VERSION</string>
  <key>CFBundleShortVersionString</key>
  <string>$APP_VERSION</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>LSMinimumSystemVersion</key>
  <string>$MIN_SYS_VER</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSPrincipalClass</key>
  <string>NSApplication</string>
</dict>
</plist>
EOF

ICON_PNG=""
for candidate in \
  "$BUNDLE_DIR/Contents/MacOS/Assets/VisualStates.png" \
  "$BUNDLE_DIR/Contents/MacOS/VisualStates.png" \
  "$REPO_ROOT/src/VisualStates/Assets/VisualStates.png"
do
  if [[ -f "$candidate" ]]; then
    ICON_PNG="$candidate"
    break
  fi
done

if [[ -n "$ICON_PNG" ]]; then
  echo "Converting icon to AppIcon.icns from $ICON_PNG"
  ICONSET="$BUNDLE_DIR/Contents/Resources/AppIcon.iconset"
  rm -rf "$ICONSET"
  mkdir -p "$ICONSET"

  for size in 16 32 64 128 256 512; do
    sips -z "$size" "$size" "$ICON_PNG" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z $((size * 2)) $((size * 2)) "$ICON_PNG" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
  done

  iconutil -c icns "$ICONSET" -o "$BUNDLE_DIR/Contents/Resources/AppIcon.icns"
  rm -rf "$ICONSET"
else
  echo "warning: icon image not found; bundle will not have a custom icon" >&2
fi

find "$PUBLISH_DIR" -mindepth 1 -maxdepth 1 ! -name "$APP_NAME.app" -exec rm -rf {} +

echo "Ad-hoc signing the application bundle..."
codesign --force --deep --sign - "$BUNDLE_DIR"

echo "=== macOS bundle ready: $BUNDLE_DIR ==="
