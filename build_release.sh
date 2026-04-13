#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$REPO_ROOT/src"
PACK_DIR="$REPO_ROOT/pack"
DIST_DIR="$REPO_ROOT/dist"
TOOLS_DIR="$REPO_ROOT/tools"
RELEASE_DIR="$REPO_ROOT/release"

MANIFEST_PATH="$PACK_DIR/mod_manifest.json"
MOD_ID="$(sed -n 's/.*"pck_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$MANIFEST_PATH" | head -n 1)"
MOD_VERSION="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$MANIFEST_PATH" | head -n 1)"

if [[ -z "${MOD_ID:-}" || -z "${MOD_VERSION:-}" ]]; then
  echo "Failed to parse pck_name/version from $MANIFEST_PATH"
  exit 1
fi

DOTNET_BIN="${DOTNET_BIN:-$(command -v dotnet || true)}"
if [[ -z "${DOTNET_BIN:-}" && -x "/usr/local/share/dotnet/dotnet" ]]; then
  DOTNET_BIN="/usr/local/share/dotnet/dotnet"
fi
if [[ -z "${DOTNET_BIN:-}" && -x "$HOME/.dotnet/dotnet" ]]; then
  DOTNET_BIN="$HOME/.dotnet/dotnet"
fi
if [[ -z "${DOTNET_BIN:-}" ]]; then
  echo "dotnet not found in PATH. Set DOTNET_BIN to your dotnet executable path."
  exit 1
fi

STS2_GAME_DIR="${STS2_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64}"
STS2_ENGINE_BIN="${STS2_ENGINE_BIN:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2}"

if [[ ! -f "$STS2_GAME_DIR/sts2.dll" ]]; then
  echo "Invalid STS2_GAME_DIR: $STS2_GAME_DIR"
  echo "Expected sts2.dll in that directory."
  exit 1
fi

if [[ ! -x "$STS2_ENGINE_BIN" ]]; then
  echo "Invalid STS2_ENGINE_BIN: $STS2_ENGINE_BIN"
  echo "Expected executable game binary to run headless for PCK build."
  exit 1
fi

mkdir -p "$DIST_DIR" "$RELEASE_DIR"

echo "Building C# DLL..."
STS2_GAME_DIR="$STS2_GAME_DIR" "$DOTNET_BIN" build "$SRC_DIR/TakuAgentMod.csproj" -c Release

echo "Packing PCK..."
"$STS2_ENGINE_BIN" --headless --script "$TOOLS_DIR/build_pck.gd" -- "$PACK_DIR" "$DIST_DIR/${MOD_ID}.pck"

PACKAGE_STAGING="$RELEASE_DIR/${MOD_ID}-v${MOD_VERSION}"
rm -rf "$PACKAGE_STAGING"
mkdir -p "$PACKAGE_STAGING"

cp -f "$DIST_DIR/${MOD_ID}.pck" "$PACKAGE_STAGING/${MOD_ID}.pck"
cp -f "$SRC_DIR/bin/Release/net9.0/${MOD_ID}.dll" "$PACKAGE_STAGING/${MOD_ID}.dll"
cp -f "$PACK_DIR/mod_manifest.json" "$PACKAGE_STAGING/mod_manifest.json"
cp -f "$REPO_ROOT/INSTALL.md" "$PACKAGE_STAGING/INSTALL.md"

ZIP_PATH="$DIST_DIR/${MOD_ID}-v${MOD_VERSION}-universal.zip"
rm -f "$ZIP_PATH"
(
  cd "$RELEASE_DIR"
  zip -rq "$ZIP_PATH" "$(basename "$PACKAGE_STAGING")"
)

echo "Done."
echo "Universal package: $ZIP_PATH"
echo "Staging dir: $PACKAGE_STAGING"
