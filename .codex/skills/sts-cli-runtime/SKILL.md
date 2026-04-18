---
name: sts-cli-runtime
description: Use when you need to build, deploy, restart, resume, or diagnose the local Slay the Spire 2 runtime that backs the `sts` CLI. Covers `build_and_deploy.sh`, `restart_game.sh`, `dev_cycle.sh`, and post-restart verification with `sts doctor`.
---

# STS CLI Runtime

Use this skill for local runtime maintenance rather than gameplay decisions.

## Runtime Workflow

1. Build and deploy with `./build_and_deploy.sh` when C# or packed files changed.
2. Restart with `./restart_game.sh --wait-for-server`.
3. Use `./dev_cycle.sh` when you want the full build -> deploy -> restart loop.
4. Verify health with `sts doctor`.
5. If needed, read `sts context` to confirm the resumed run landed in the expected state.

## Preferred Commands

- `./build_and_deploy.sh`
- `./restart_game.sh --wait-for-server`
- `./dev_cycle.sh`
- `./dev_cycle.sh --smoke`
- `sts doctor`
- `sts context`

## Rules

- Prefer `./dev_cycle.sh --smoke` after risky CLI or server changes.
- `restart_game.sh` auto-resumes the saved run unless `STS2_AUTO_CONTINUE=0` or `--no-auto-continue` is used.
- If `sts doctor` reports `observer_port=false`, restart the game before deeper debugging.
- If `sts doctor` reports `game_process=false`, the issue is local runtime, not CLI command routing.
