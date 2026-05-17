#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SERVER_TIMEOUT_SECONDS=90
RUN_SMOKE=0
SKIP_BUILD=0
SKIP_RESTART=0

usage() {
  cat <<'EOF'
Usage:
  scripts/dev/dev-cycle.sh
  scripts/dev/dev-cycle.sh --smoke
  scripts/dev/dev-cycle.sh --server-timeout 120
  scripts/dev/dev-cycle.sh --skip-build
  scripts/dev/dev-cycle.sh --skip-restart

Behavior:
  1. Build and deploy the mod
  2. Restart Slay the Spire 2
  3. Wait for the observer server to come back
  4. Optionally run a minimal smoke check

Environment:
  STS2_APP_PATH      Forwarded to scripts/dev/restart-game.sh
  STS2_GAME_BIN      Forwarded to scripts/dev/restart-game.sh
  STS2_OBSERVER_URL  Forwarded to scripts/dev/restart-game.sh and ./sts
  STS2_STEAM_APP_ID  Forwarded to scripts/dev/restart-game.sh
  STS2_LAUNCH_DIRECT Forwarded to scripts/dev/restart-game.sh
  STS2_AUTO_CONTINUE Forwarded to scripts/dev/restart-game.sh
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --smoke)
      RUN_SMOKE=1
      shift
      ;;
    --server-timeout)
      SERVER_TIMEOUT_SECONDS="${2:-}"
      shift 2
      ;;
    --server-timeout=*)
      SERVER_TIMEOUT_SECONDS="${1#*=}"
      shift
      ;;
    --skip-build)
      SKIP_BUILD=1
      shift
      ;;
    --skip-restart)
      SKIP_RESTART=1
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

if ! [[ "$SERVER_TIMEOUT_SECONDS" =~ ^[0-9]+$ ]]; then
  echo "SERVER_TIMEOUT_SECONDS must be an integer: $SERVER_TIMEOUT_SECONDS" >&2
  exit 1
fi

cd "$REPO_ROOT"

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  echo "==> Building and deploying"
  scripts/dev/build-and-deploy.sh
fi

if [[ "$SKIP_RESTART" -eq 0 ]]; then
  echo "==> Restarting game"
  scripts/dev/restart-game.sh --wait-for-server --server-timeout "$SERVER_TIMEOUT_SECONDS"
fi

if [[ "$RUN_SMOKE" -eq 1 ]]; then
  echo "==> Running smoke checks"
  ./sts ping
  ./sts context
  ./sts actions
  ./sts doctor
fi

echo "Development cycle complete."
