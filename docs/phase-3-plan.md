# Phase 3 Plan

## Goal

Phase 3 turns the completed Phase 2 execution layer into a run-level planning surface.

The target is no longer "can the agent legally act in the current room" but:

- can the agent keep a coherent run plan across multiple rooms
- can it make low-token strategic decisions before acting
- can it reuse compact summaries instead of rebuilding context from raw endpoints every turn

Phase 3 should stay low-token-first. It should add planning-friendly aggregation and skills, not a second monolithic state API.

## Starting Point

Phase 2 is now treated as stable enough to build on:

- `context`, `actions`, and `actions/execute` are usable in live gameplay
- `wait`, `exec --wait-for`, and default wait templates are in place
- room transitions across menu, event, map, combat, rewards, and card rewards have been validated
- `skip_card_reward` is explicitly supported and validated in a real run
- npm-packaged `sts` and repo-local skills already exist

## Phase 3 Outcomes

Phase 3 is successful when an agent can:

1. resume a run and understand the current strategic state in one or two reads
2. choose map nodes with awareness of health, gold, potions, relics, and near-term path structure
3. evaluate room choices without reading every low-level endpoint by default
4. keep combat decisions constrained by run-level priorities
5. operate through Codex skills that encode stable workflows instead of ad hoc command strings

## Design Principles

- Prefer aggregated planning views over repeated raw endpoint fan-out.
- Keep combat tactics and run planning separated.
- Preserve narrow endpoints as building blocks.
- Make recommendations explainable with compact fields.
- Keep CLI output machine-friendly first, prose-friendly second.

## Batch 1

Batch 1 is the minimum Phase 3 bootstrap layer.

### 1. Add `phase-3-plan.md`

This document defines the initial planning contract, milestones, and acceptance criteria.

### 2. Add a run-level planning skill

Create a dedicated Codex skill for strategic play:

- use `sts run snapshot` as the default first read
- route to combat or room-flow skills only after a strategic decision is made
- encode conservative baseline heuristics

### 3. Add `sts run snapshot`

Create a high-level planning read that combines:

- `context`
- `run`
- `player summary`
- `actions`
- `room summary`
- `compact observation`
- embedded combat snapshot when currently in combat

This command should be the default "what should I think about next" read for Phase 3.

### 4. Document the recommended planning loop

Document a default loop:

1. `sts run snapshot`
2. identify strategic decision type
3. expand one narrow endpoint only if required
4. choose one legal action
5. `sts exec ...`
6. repeat from `sts run snapshot` after the room stabilizes

## Batch 2

Once Batch 1 is stable, add planning-specific aggregation:

- `sts map routes`
- reward evaluation summaries
- shop evaluation summaries
- rest-site evaluation summaries
- build/deck summary views that avoid dumping full card text

## Batch 3

Add reusable planning outputs that can be consumed by different agent styles:

- compact recommendation fields
- ranked candidate lists with short reasons
- explicit risk/reward axes
- room-type-specific strategy hints

## CLI Contract For Planning

Planning commands should follow these rules:

- one top-level command should provide the default planning view
- room-specific expansion stays opt-in
- output should contain enough typed fields to support simple policy code
- command names should stay short and composable

Batch 1 command:

- `sts run snapshot`

Planned follow-ups:

- `sts map routes`
- `sts rewards resolve`
- `sts shop evaluate`
- `sts rest-site evaluate`

## Skill Contract For Planning

The planning skill should:

- start from `sts run snapshot`
- prefer low-risk heuristics when no stronger build signal exists
- treat skip flows as valid strategic options
- hand combat execution to the combat skill
- hand non-combat room execution to the room-flow skill

## Acceptance Criteria

Batch 1 is complete when:

- `sts run snapshot` works from menu and in-run rooms
- the planning skill exists and points agents to the new entrypoint
- README documents the planning entrypoint and loop
- CLI tests cover the new command

Phase 3 as a whole is complete when:

- map, reward, shop, and rest decisions can be made from planning-friendly summaries
- agents can explain strategic choices with compact evidence
- the common planning path does not require full deck dumps or full-state reads
