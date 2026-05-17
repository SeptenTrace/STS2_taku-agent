#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MOD_SRC_DIR="$REPO_ROOT/mod/src"
MOD_PACK_DIR="$REPO_ROOT/mod/pack"
DIST_DIR="$REPO_ROOT/dist"
RELEASE_DIR="$DIST_DIR/release"
STAGING_DIR="$DIST_DIR/staging"
CLI_RELEASE_DIR="$RELEASE_DIR/cli"
MOD_RELEASE_DIR="$RELEASE_DIR/mod"
BUILD_PCK_SCRIPT="$REPO_ROOT/scripts/release/build_pck.gd"

MANIFEST_PATH="$MOD_PACK_DIR/mod_manifest.json"
MOD_ID="$(node -e 'process.stdout.write(JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8")).id)' "$MANIFEST_PATH")"
MOD_VERSION="$(node -e 'process.stdout.write(JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8")).version)' "$MANIFEST_PATH")"
CLI_VERSION="$(node -e 'process.stdout.write(JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8")).version)' "$REPO_ROOT/package.json")"

if [[ -z "${MOD_ID:-}" || -z "${MOD_VERSION:-}" ]]; then
  echo "Failed to parse id/version from $MANIFEST_PATH"
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

DEFAULT_STS2_GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64"
if [[ ! -f "$DEFAULT_STS2_GAME_DIR/sts2.dll" ]]; then
  DEFAULT_STS2_GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_x86_64"
fi

STS2_GAME_DIR="${STS2_GAME_DIR:-$DEFAULT_STS2_GAME_DIR}"
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

create_zip() {
  local source_dir="$1"
  local zip_path="$2"

  rm -f "$zip_path"
  (
    cd "$source_dir"
    zip -rq "$zip_path" .
  )
}

write_checksum() {
  local artifact="$1"

  (
    cd "$(dirname "$artifact")"
    shasum -a 256 "$(basename "$artifact")" > "$(basename "$artifact").sha256"
  )
}

stage_cli_package() {
  local platform="$1"
  local extension="$2"
  local script_name="install.${extension}"
  local package_name="sts-cli-v${CLI_VERSION}-${platform}"
  local stage_dir="$STAGING_DIR/$package_name"
  local zip_path="$CLI_RELEASE_DIR/${package_name}.zip"

  rm -rf "$stage_dir"
  mkdir -p "$stage_dir"
  cp -f "$CLI_TGZ_PATH" "$stage_dir/"

  if [[ "$extension" == "sh" ]]; then
    cat > "$stage_dir/$script_name" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_PATH="$(find "$SCRIPT_DIR" -maxdepth 1 -name '*.tgz' | head -n 1)"
if [[ -z "${PACKAGE_PATH:-}" ]]; then
  echo "Could not find the sts CLI npm package next to this installer." >&2
  exit 1
fi
npm install -g "$PACKAGE_PATH"
sts help
EOF
    chmod +x "$stage_dir/$script_name"
  else
    cat > "$stage_dir/$script_name" <<'EOF'
$ErrorActionPreference = "Stop"
$packagePath = Get-ChildItem -Path $PSScriptRoot -Filter "*.tgz" | Select-Object -First 1
if (-not $packagePath) {
  throw "Could not find the sts CLI npm package next to this installer."
}
npm install -g $packagePath.FullName
sts help
EOF
  fi

  if [[ "$extension" == "sh" ]]; then
    local install_command="./install.sh"
  else
    local install_command=".\\install.ps1"
  fi

  cat > "$stage_dir/README.txt" <<EOF
STS2 Taku Agent CLI ${CLI_VERSION}

Install:
- ${install_command}

Requires Node.js 24 or newer.
EOF

  create_zip "$stage_dir" "$zip_path"
  write_checksum "$zip_path"
}

stage_mod_package() {
  local platform="$1"
  local package_name="${MOD_ID}-v${MOD_VERSION}-${platform}"
  local stage_dir="$STAGING_DIR/$package_name"
  local mod_dir="$stage_dir/$MOD_ID"
  local zip_path="$MOD_RELEASE_DIR/${package_name}.zip"

  rm -rf "$stage_dir"
  mkdir -p "$mod_dir"
  cp -f "$DIST_DIR/${MOD_ID}.pck" "$mod_dir/${MOD_ID}.pck"
  cp -f "$MOD_SRC_DIR/bin/Release/net9.0/${MOD_ID}.dll" "$mod_dir/${MOD_ID}.dll"
  cp -f "$MANIFEST_PATH" "$mod_dir/mod_manifest.json"
  cp -f "$REPO_ROOT/INSTALL.md" "$stage_dir/INSTALL.md"

  create_zip "$stage_dir" "$zip_path"
  write_checksum "$zip_path"
}

rm -rf "$RELEASE_DIR" "$STAGING_DIR"
mkdir -p "$DIST_DIR" "$CLI_RELEASE_DIR" "$MOD_RELEASE_DIR" "$STAGING_DIR"

echo "Building CLI package..."
(
  cd "$REPO_ROOT"
  npm run build:cli
)

NPM_PACK_OUTPUT="$(cd "$REPO_ROOT" && npm pack --pack-destination "$CLI_RELEASE_DIR" --silent)"
CLI_TGZ_PATH="$CLI_RELEASE_DIR/$(printf '%s\n' "$NPM_PACK_OUTPUT" | tail -n 1)"

stage_cli_package "macos" "sh"
stage_cli_package "windows" "ps1"

echo "Building C# DLL..."
STS2_GAME_DIR="$STS2_GAME_DIR" "$DOTNET_BIN" build "$MOD_SRC_DIR/TakuAgentMod.csproj" -c Release

echo "Packing PCK..."
"$STS2_ENGINE_BIN" --headless --script "$BUILD_PCK_SCRIPT" -- "$MOD_PACK_DIR" "$DIST_DIR/${MOD_ID}.pck"

stage_mod_package "macos"
stage_mod_package "windows"

write_checksum "$CLI_TGZ_PATH"

echo "Done."
find "$RELEASE_DIR" -maxdepth 2 -type f | sort
