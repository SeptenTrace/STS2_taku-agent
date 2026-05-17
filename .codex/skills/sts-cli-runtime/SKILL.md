---
name: sts-cli-runtime
description: Use when you need to build, deploy, restart, resume, or diagnose the local Slay the Spire 2 runtime that backs the `sts` CLI. Covers `scripts/dev/build-and-deploy.sh`, `scripts/dev/restart-game.sh`, `scripts/dev/dev-cycle.sh`, and post-restart verification with `sts doctor`.
---

# STS CLI Runtime

Use this skill for local runtime maintenance rather than gameplay decisions.

## Runtime Workflow

1. Build and deploy with `scripts/dev/build-and-deploy.sh` when C# or packed files changed.
2. Restart with `scripts/dev/restart-game.sh --wait-for-server`.
3. Use `scripts/dev/dev-cycle.sh` when you want the full build -> deploy -> restart loop.
4. Verify health with `sts doctor`.
5. If needed, read `sts context` to confirm the resumed run landed in the expected state.

## Preferred Commands

- `scripts/dev/build-and-deploy.sh`
- `scripts/dev/restart-game.sh --wait-for-server`
- `scripts/dev/dev-cycle.sh`
- `scripts/dev/dev-cycle.sh --smoke`
- `sts doctor`
- `sts context`

## Rules

- Prefer `scripts/dev/dev-cycle.sh --smoke` after risky CLI or server changes.
- `scripts/dev/restart-game.sh` auto-resumes the saved run unless `STS2_AUTO_CONTINUE=0` or `--no-auto-continue` is used.
- If `sts doctor` reports `observer_port=false`, restart the game before deeper debugging.
- If `sts doctor` reports `game_process=false`, the issue is local runtime, not CLI command routing.
