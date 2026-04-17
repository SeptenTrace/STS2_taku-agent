#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
STS2_APP_PATH="${STS2_APP_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
STS2_GAME_BIN="${STS2_GAME_BIN:-$STS2_APP_PATH/Contents/MacOS/Slay the Spire 2}"
STS_OBSERVER_URL="${STS_OBSERVER_URL:-http://127.0.0.1:15527}"
STS2_STEAM_APP_ID="${STS2_STEAM_APP_ID:-2868840}"
STS2_LAUNCH_DIRECT="${STS2_LAUNCH_DIRECT:-0}"

WAIT_FOR_SERVER=0
SERVER_TIMEOUT_SECONDS=90
EXIT_TIMEOUT_SECONDS=20
FORCE_KILL=1

usage() {
  cat <<'EOF'
Usage:
  ./restart_game.sh
  ./restart_game.sh --wait-for-server
  ./restart_game.sh --wait-for-server --server-timeout 120
  ./restart_game.sh --no-force-kill

Environment:
  STS2_APP_PATH      Override the Slay the Spire 2 app bundle path.
  STS2_GAME_BIN      Override the game binary path.
  STS_OBSERVER_URL   Override the observer base URL. Default: http://127.0.0.1:15527
  STS2_STEAM_APP_ID  Override the Steam app id. Default: 2868840
  STS2_LAUNCH_DIRECT Set to 1 to launch the .app directly instead of using Steam.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --wait-for-server)
      WAIT_FOR_SERVER=1
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
    --no-force-kill)
      FORCE_KILL=0
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

if [[ ! -d "$STS2_APP_PATH" ]]; then
  echo "Invalid STS2_APP_PATH: $STS2_APP_PATH" >&2
  exit 1
fi

APP_NAME="$(basename "$STS2_APP_PATH" .app)"

collect_game_pids() {
  local -a pids=()
  local pid
  while IFS= read -r pid; do
    [[ -n "$pid" ]] && pids+=("$pid")
  done < <(pgrep -f "$STS2_GAME_BIN" || true)

  if [[ ${#pids[@]} -eq 0 ]]; then
    while IFS= read -r pid; do
      [[ -n "$pid" ]] && pids+=("$pid")
    done < <(pgrep -x "$APP_NAME" || true)
  fi

  if [[ ${#pids[@]} -eq 0 ]]; then
    return 0
  fi

  printf '%s\n' "${pids[@]}" | awk '!seen[$0]++'
}

wait_for_exit() {
  local deadline=$((SECONDS + EXIT_TIMEOUT_SECONDS))
  while [[ $SECONDS -lt $deadline ]]; do
    if [[ -z "$(collect_game_pids)" ]]; then
      return 0
    fi
    sleep 1
  done
  return 1
}

wait_for_server() {
  local deadline=$((SECONDS + SERVER_TIMEOUT_SECONDS))
  while [[ $SECONDS -lt $deadline ]]; do
    if curl -fsS "${STS_OBSERVER_URL%/}/" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  return 1
}

existing_pids="$(collect_game_pids)"
if [[ -n "$existing_pids" ]]; then
  echo "Stopping $APP_NAME..."
  if command -v osascript >/dev/null 2>&1; then
    osascript -e "tell application \"$APP_NAME\" to quit" >/dev/null 2>&1 || true
  fi

  sleep 2

  remaining_pids="$(collect_game_pids)"
  if [[ -n "$remaining_pids" ]]; then
    while IFS= read -r pid; do
      [[ -n "$pid" ]] && kill -TERM "$pid" || true
    done <<< "$remaining_pids"
  fi

  if ! wait_for_exit; then
    remaining_pids="$(collect_game_pids)"
    if [[ -n "$remaining_pids" ]]; then
      if [[ "$FORCE_KILL" -eq 1 ]]; then
        echo "Force killing remaining game processes..."
        while IFS= read -r pid; do
          [[ -n "$pid" ]] && kill -KILL "$pid" || true
        done <<< "$remaining_pids"
      else
        echo "Game did not exit within ${EXIT_TIMEOUT_SECONDS}s." >&2
        exit 1
      fi
    fi
  fi
fi

echo "Starting $APP_NAME..."
if [[ "$STS2_LAUNCH_DIRECT" == "1" ]]; then
  open "$STS2_APP_PATH"
else
  open "steam://run/${STS2_STEAM_APP_ID}"
fi

if [[ "$WAIT_FOR_SERVER" -eq 1 ]]; then
  echo "Waiting for observer server at ${STS_OBSERVER_URL%/}/ ..."
  if ! wait_for_server; then
    echo "Timed out waiting for observer server after ${SERVER_TIMEOUT_SECONDS}s." >&2
    exit 1
  fi
  echo "Observer server is ready."
fi

echo "Game restart complete."
