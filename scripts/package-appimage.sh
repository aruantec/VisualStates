#!/usr/bin/env bash
# Package a published Linux build into an AppImage.
set -euo pipefail

PUBLISH_DIR="${1:-}"
OUTPUT_DIR="${2:-}"
TARGET_ARCH="${3:-}"
OUTPUT_NAME="${4:-}"

if [[ -z "$PUBLISH_DIR" || -z "$OUTPUT_DIR" ]]; then
  echo "usage: $0 <publish-dir> <output-dir> [x86_64|aarch64] [output-name]" >&2
  exit 1
fi

PUBLISH_DIR="$(cd "$PUBLISH_DIR" && pwd)"
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_NAME="VisualStates"
EXECUTABLE_NAME="VisualStates"
DESKTOP_TEMPLATE="$REPO_ROOT/packaging/linux/visualstates.desktop"
ICON_FILE="$REPO_ROOT/src/VisualStates/Assets/VisualStates.png"
APP_VERSION="${APP_VERSION:-${VERSION:-${GITHUB_REF_NAME:-dev}}}"
APP_VERSION="${APP_VERSION#v}"

if [[ ! "$APP_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?$ ]]; then
  APP_VERSION="0.0.0-local"
fi

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "publish directory not found: $PUBLISH_DIR" >&2
  exit 1
fi

if [[ ! -f "$PUBLISH_DIR/$EXECUTABLE_NAME" ]]; then
  echo "published executable not found: $PUBLISH_DIR/$EXECUTABLE_NAME" >&2
  exit 1
fi

if [[ ! -f "$DESKTOP_TEMPLATE" ]]; then
  echo "desktop template not found: $DESKTOP_TEMPLATE" >&2
  exit 1
fi

if [[ ! -f "$ICON_FILE" ]]; then
  echo "icon not found: $ICON_FILE" >&2
  exit 1
fi

if [[ -z "$TARGET_ARCH" ]]; then
  case "$(uname -m)" in
    x86_64|amd64) TARGET_ARCH="x86_64" ;;
    aarch64|arm64) TARGET_ARCH="aarch64" ;;
    *)
      echo "unsupported architecture: $(uname -m)" >&2
      exit 1
      ;;
  esac
fi

case "$TARGET_ARCH" in
  x86_64|aarch64) ;;
  *)
    echo "unsupported AppImage architecture: $TARGET_ARCH" >&2
    exit 1
    ;;
esac

if [[ -n "${APPIMAGETOOL:-}" ]]; then
  APPIMAGETOOL_BIN="$APPIMAGETOOL"
elif command -v appimagetool >/dev/null 2>&1; then
  APPIMAGETOOL_BIN="$(command -v appimagetool)"
else
  echo "appimagetool not found. Set APPIMAGETOOL or add it to PATH." >&2
  exit 1
fi

APPIMAGETOOL_BIN="$(cd "$(dirname "$APPIMAGETOOL_BIN")" && pwd)/$(basename "$APPIMAGETOOL_BIN")"

WORK_DIR="$(mktemp -d)"
APPDIR="$WORK_DIR/${APP_NAME}.AppDir"
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

mkdir -p \
  "$APPDIR/usr/bin" \
  "$APPDIR/usr/share/visualstates" \
  "$APPDIR/usr/share/applications" \
  "$APPDIR/usr/share/icons/hicolor/256x256/apps"

cp -a "$PUBLISH_DIR/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/$EXECUTABLE_NAME"

DESKTOP_FILE="$WORK_DIR/visualstates.desktop"
sed \
  -e "s|__EXEC__|${EXECUTABLE_NAME}|g" \
  -e "s|__ICON__|visualstates|g" \
  "$DESKTOP_TEMPLATE" > "$DESKTOP_FILE"

cp "$DESKTOP_FILE" "$APPDIR/visualstates.desktop"
cp "$DESKTOP_FILE" "$APPDIR/usr/share/applications/visualstates.desktop"
cp "$ICON_FILE" "$APPDIR/visualstates.png"
cp "$ICON_FILE" "$APPDIR/usr/share/icons/hicolor/256x256/apps/visualstates.png"
printf '%s\n' "$APP_VERSION" > "$APPDIR/usr/share/visualstates/version.txt"

cat > "$APPDIR/AppRun" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
export APPDIR="$(cd "$(dirname "$0")" && pwd)"
if [[ -z "${APPIMAGE:-}" && -n "${ARGV0:-}" ]]; then
  export APPIMAGE="$ARGV0"
fi
exec "$APPDIR/usr/bin/VisualStates" "$@"
EOF
chmod +x "$APPDIR/AppRun"

if [[ -n "$OUTPUT_NAME" ]]; then
  OUTPUT_FILE="$OUTPUT_DIR/$OUTPUT_NAME"
elif [[ "$TARGET_ARCH" == "x86_64" ]]; then
  OUTPUT_FILE="$OUTPUT_DIR/${APP_NAME}-Linux-${TARGET_ARCH}.AppImage"
else
  OUTPUT_FILE="$OUTPUT_DIR/${APP_NAME}-${TARGET_ARCH}.AppImage"
fi

export ARCH="$TARGET_ARCH"
export VERSION="$APP_VERSION"
export APPIMAGE_EXTRACT_AND_RUN=1

"$APPIMAGETOOL_BIN" --no-appstream "$APPDIR" "$OUTPUT_FILE"
chmod +x "$OUTPUT_FILE"

echo "Created AppImage: $OUTPUT_FILE"
