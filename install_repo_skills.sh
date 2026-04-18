#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
SOURCE_DIR="${REPO_ROOT}/.codex/skills"
CODEX_HOME_DIR="${CODEX_HOME:-$HOME/.codex}"
TARGET_DIR="${CODEX_HOME_DIR}/skills"

usage() {
  cat <<'EOF'
Usage:
  ./install_repo_skills.sh
  ./install_repo_skills.sh --copy

Behavior:
  Installs the repo-local STS CLI skills into the active Codex skills directory.
  Default mode creates symlinks so skill edits in this repo are reflected immediately.

Environment:
  CODEX_HOME  Override the Codex home directory. Default: ~/.codex
EOF
}

MODE="symlink"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --copy)
      MODE="copy"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

mkdir -p "$TARGET_DIR"

find "$SOURCE_DIR" -mindepth 1 -maxdepth 1 -type d | while read -r skill_dir; do
  skill_name="$(basename "$skill_dir")"
  target_path="${TARGET_DIR}/${skill_name}"

  rm -rf "$target_path"
  if [[ "$MODE" == "copy" ]]; then
    cp -R "$skill_dir" "$target_path"
    echo "Copied ${skill_name} -> ${target_path}"
  else
    ln -s "$skill_dir" "$target_path"
    echo "Linked ${skill_name} -> ${target_path}"
  fi
done
