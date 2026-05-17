#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MOD_SRC_DIR="$REPO_ROOT/mod/src"
MOD_PACK_DIR="$REPO_ROOT/mod/pack"
DIST_DIR="$REPO_ROOT/dist"
BUILD_PCK_SCRIPT="$REPO_ROOT/scripts/release/build_pck.gd"
MANIFEST_PATH="$MOD_PACK_DIR/mod_manifest.json"

GAME_DATA_DIR="${STS2_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64}"
GAME_MODS_DIR="${STS2_MODS_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods}"
DOTNET_BIN="${DOTNET_BIN:-$(command -v dotnet || true)}"
GAME_ENGINE_BIN="${STS2_ENGINE_BIN:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2}"
MOD_ID="$(sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$MANIFEST_PATH" | head -n 1)"

if [[ -z "${MOD_ID:-}" ]]; then
  echo "Failed to parse id from $MANIFEST_PATH"
  exit 1
fi

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

MOD_DEPLOY_DIR="$GAME_MODS_DIR/$MOD_ID"

mkdir -p "$DIST_DIR" "$MOD_DEPLOY_DIR"

STS2_GAME_DIR="$GAME_DATA_DIR" "$DOTNET_BIN" build "$MOD_SRC_DIR/TakuAgentMod.csproj" -c Release

"$GAME_ENGINE_BIN" --headless --script "$BUILD_PCK_SCRIPT" -- "$MOD_PACK_DIR" "$DIST_DIR/${MOD_ID}.pck"

cp -f "$DIST_DIR/${MOD_ID}.pck" "$MOD_DEPLOY_DIR/${MOD_ID}.pck"
cp -f "$MOD_SRC_DIR/bin/Release/net9.0/${MOD_ID}.dll" "$MOD_DEPLOY_DIR/${MOD_ID}.dll"
cp -f "$MANIFEST_PATH" "$MOD_DEPLOY_DIR/mod_manifest.json"

echo "Done. Deployed to: $MOD_DEPLOY_DIR"
