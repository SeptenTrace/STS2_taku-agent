---
name: sts-cli-observe
description: Use when you need read-only inspection of Slay the Spire 2 through the global `sts` CLI. Covers `sts doctor`, `sts context`, `sts menu`, `sts room summary`, narrow read endpoints, and `sts wait` for stable-state synchronization without mutating gameplay.
---

# STS CLI Observe

Use this skill to inspect the current game state with the fewest possible CLI calls.

## Workflow

1. Start with `sts doctor` if connectivity or state sync might be broken.
2. Read `sts context` to identify `stateType`, `isStable`, and `isTransitioning`.
3. Prefer `sts room summary` as the default compact read for any stable in-run room.
4. Only fan out to narrow endpoints when the decision needs more detail.
5. If the screen is transitioning, use `sts wait ...` before reading deeper state.

## Preferred Commands

- `sts doctor`
- `sts context`
- `sts menu`
- `sts room summary`
- `sts actions`
- `sts wait CONDITION`
- `sts wait CONDITION --verbose`

## Narrow Endpoint Guide

- Combat: `sts combat summary`, `sts combat enemies`, `sts combat hand`
- Map: `sts map`
- Rewards: `sts rewards`, `sts card-reward`
- Events and shops: `sts event`, `sts fake-merchant`, `sts shop`, `sts rest-site`, `sts treasure`
- Rare overlays: `sts overlay`, `sts crystal-sphere`, `sts bundle-selection`, `sts relic-selection`

## Rules

- Do not use `sts exec ...` in this skill unless the user explicitly asks to act.
- Treat `isTransitioning=true` as non-actionable.
- Prefer one compact read plus one narrow read over repeated full summaries.
- Use `sts wait room-ready --verbose` when diagnosing flaky transitions.
