#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$REPO_ROOT/src"
PACK_DIR="$REPO_ROOT/pack"
DIST_DIR="$REPO_ROOT/dist"
TOOLS_DIR="$REPO_ROOT/tools"

GAME_DATA_DIR="${STS2_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64}"
GAME_MODS_DIR="${STS2_MODS_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods}"
DOTNET_BIN="${DOTNET_BIN:-$(command -v dotnet || true)}"
GAME_ENGINE_BIN="${STS2_ENGINE_BIN:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2}"

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

mkdir -p "$DIST_DIR" "$GAME_MODS_DIR"

STS2_GAME_DIR="$GAME_DATA_DIR" "$DOTNET_BIN" build "$SRC_DIR/TakuAgentMod.csproj" -c Release

"$GAME_ENGINE_BIN" --headless --script "$TOOLS_DIR/build_pck.gd" -- "$PACK_DIR" "$DIST_DIR/taku_agent.pck"

cp -f "$DIST_DIR/taku_agent.pck" "$GAME_MODS_DIR/taku_agent.pck"
cp -f "$SRC_DIR/bin/Release/net9.0/taku_agent.dll" "$GAME_MODS_DIR/taku_agent.dll"

echo "Done. Deployed to: $GAME_MODS_DIR"
