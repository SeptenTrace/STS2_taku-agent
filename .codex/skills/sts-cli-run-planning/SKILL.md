---
name: sts-cli-run-planning
description: Use when the task is to plan or guide a Slay the Spire 2 run above the room-action level. Starts from `sts run snapshot`, identifies the current strategic decision, and then hands execution to the narrower combat or room-flow skills.
---

# STS CLI Run Planning

Use this skill when the question is strategic rather than purely local.

Examples:

- choose a route
- decide whether to skip a reward
- judge whether to rest, upgrade, buy, or save gold
- continue a run with a coherent plan instead of a one-off action

## Default Workflow

1. Start with `sts run snapshot`.
2. Identify the current decision type from `context.stateType`, `run`, and `actions`.
3. Expand one narrow endpoint only if the current room needs more detail.
4. Form a short strategic recommendation.
5. Hand execution to the narrower skill that matches the room:
   combat -> `sts-cli-combat`
   non-combat room -> `sts-cli-room-flow`

## Preferred Commands

- `sts run snapshot`
- `sts room summary`
- `sts map`
- `sts rewards`
- `sts card-reward`
- `sts shop`
- `sts rest-site`
- `sts event`
- `sts actions`

## Planning Rules

- Prefer `sts run snapshot` over manually stitching `run + room + player + compact`.
- Treat `isTransitioning=true` as non-actionable and wait before planning deeper.
- Use `sts room summary` only as a lighter fallback when full run context is not needed.
- Expand `sts map` for routing, `sts card-reward` for reward choices, and `sts shop` for purchases only when those rooms are active.

## Baseline Heuristics

- Default to conservative play when no stronger build signal exists.
- Treat `skip_card_reward` as a first-class option when the reward does not clearly improve the deck.
- Avoid spending gold just because a shop is available.
- Prefer stable, low-risk map progression over speculative route greed unless the run state clearly supports risk.

## Hand-off Rules

- If the current room is combat, switch to `sts-cli-combat` before acting.
- If the current room is a stable non-combat room, switch to `sts-cli-room-flow` before acting.
- Use this skill to decide what the run should optimize for, not to micro-execute every step itself.
