# STS2 Taku Agent

Starter repository for a Slay the Spire 2 mod focused on building an AI-playable interface for Slay the Spire 2.

## Planning Docs
- `docs/overall-plan.md`: project roadmap across the three major phases
- `docs/phase-1-observer.md`: detailed plan for building the game-state observation layer
- `docs/phase-1-api.md`: low-token API design for phase 1 observation
- `docs/phase-2-todo.md`: action-layer TODO list for phase 2
- `docs/phase-3-todo.md`: planning-layer TODO list for phase 3
- `docs/feasibility/README.md`: feasibility assessment for the three planned phases

## Repository Layout
- `src/`: C# mod source
- `pack/`: files packaged into the `.pck`
- `tools/`: helper scripts for packaging
- `tools/sts-cli/`: TypeScript implementation of the repo-local CLI
- `build_and_deploy.sh`: build locally and copy outputs into the game `mods` folder
- `restart_game.sh`: stop and relaunch the local macOS game client, optionally waiting for the observer server
- `dev_cycle.sh`: build, deploy, restart, wait for the observer server, and optionally run smoke checks
- `build_release.sh`: build a shareable release package

## Current Status
Phase 1 is complete as the observation layer, and Phase 2 is now implemented and validated in live gameplay on top of the same low-token contract.

The repository includes a working runtime capture pipeline, a low-token localhost observation server, a repo-local CLI, semantic action surfaces, incremental delta reads, a current-context knowledge cache, and a Phase 2 execution endpoint that returns linked observation deltas plus recovery guidance.

Recent reliability work also added:

- stable-vs-transitioning state signals on `context`
- stricter action hiding during transient or non-actionable frames
- a repo-local wait primitive and `exec --wait-for ...` follow-up flow

## Current Design

Phase 1 currently follows a `CLI + server + layered observation` design:

- `server`
  The mod exposes a localhost read-only observation server.
- `cli`
  The repository provides `./sts` as a stable local command surface on top of the server.
- `layered observation`
  The default query path is `context -> compact observation -> narrow endpoint`, not `full state`.

This is intentionally optimized for agent usage rather than for generic debugging.

## Reference And Direction

`STS2MCP` is currently our best reference source for:

- Hook discovery
- Global `stateType` classification
- Room / overlay aware state building
- Practical runtime object access patterns

But `STS2_taku-agent` has a more ambitious goal in one specific direction:

- reduce token consumption as aggressively as possible

So the design target here is not only:

- can the mod read the game state

but also:

- can an agent query only the state it actually needs
- can it avoid repeated long text and repeated full-state reads
- can it make a decision with the fewest possible tokens and round trips

In short:

- learn state coverage from `STS2MCP`
- but build a more opinionated low-token observation layer on top
- then use that observation layer as the foundation for later action APIs

## Completed In Phase 1

Currently completed:

- Battle snapshot capture on `combat_setup`
- Battle snapshot capture on `after_player_turn_start`
- Battle snapshot capture on `after_card_played`
- Action history logging for played cards
- Runtime JSON snapshot export for validation
- Local observation server on `localhost:15527`
- Global `context` classification endpoint
- `compact observation` endpoint for minimal decision context
- `delta observation` endpoint for incremental post-action reads
- generic `actions` endpoint for the current screen
- `actions/execute` write endpoint that reuses the same action names and parameter contract
- `knowledge/current` endpoint that separates current-context card / relic / potion / status knowledge from dynamic state
- Fine-grained read-only endpoints for combat, player, map, rewards, event, shop, rest-site, treasure, and card selection
- `combat/actions` endpoint exposing legal card actions, potion actions, legal target sets, and semantic summaries
- Lightweight `player/summary` endpoint without full deck payload
- Structured combat summary with `incomingDamage`, `playableCards`, potion action count, and total action count
- Repo-local `./sts` CLI wrapper over the observation server
- Phase 2 execution endpoint for combat, map, rewards, shop, rest-site, treasure, and card selection actions

Currently verified fields:
- Player role / character type
- Player HP, max HP, block, energy, max energy, stars
- Player status effects with title, amount, description, and category
- Enemy HP, max HP, block, alive/hittable state, buffs/debuffs, and current intent summary
- Hand, draw pile, discard pile, and exhaust pile contents
- Card title, description, type, rarity, target type, resolved energy cost, X-cost flag, star cost, upgrade state, and keywords
- Potion title, usage, target type, rarity, and effect description
- Relic title, rarity, counter, and effect description

Current capture triggers:
- `combat_setup`
- `after_player_turn_start`
- `after_card_played`

Snapshot output path on macOS:
- `~/Library/Application Support/STS2TakuAgent/phase1-feasibility/`

Action history output:
- `~/Library/Application Support/STS2TakuAgent/phase1-feasibility/action-history.jsonl`

Observation server:
- `http://localhost:15527/`
- `http://localhost:15527/api/v1/capabilities`
- `http://localhost:15527/api/v1/context`
- `http://localhost:15527/api/v1/observation/compact`
- `http://localhost:15527/api/v1/observation/delta`
- `http://localhost:15527/api/v1/actions`
- `http://localhost:15527/api/v1/actions/execute`
- `http://localhost:15527/api/v1/knowledge/current`
- `http://localhost:15527/api/v1/combat/actions`
- `http://localhost:15527/api/v1/fake-merchant`
- `http://localhost:15527/api/v1/bundle-selection`
- `http://localhost:15527/api/v1/relic-selection`
- `http://localhost:15527/api/v1/crystal-sphere`
- `http://localhost:15527/api/v1/overlay`

`/api/v1/context` now includes:
- `stateType`
- `roomType`
- `overlayType`
- `isStable`
- `isTransitioning`
- `recommendedQueries`

`isStable=true` means the current screen is ready for agent decisions.
`isTransitioning=true` means the mod is intentionally treating the current frame as an in-between state, and `/api/v1/actions` may be empty until the room settles.

Local CLI:
- `./sts ping`
- `./sts next`
- `./sts delta`
- `./sts actions`
- `./sts exec play_card 0 jaw_worm_0`
- `./sts exec select_card 1`
- `./sts exec end_turn`
- `./sts combat actions`
- `./sts player summary`
- `./sts knowledge current`
- `./sts fake-merchant`
- `./sts bundle-selection`
- `./sts relic-selection`
- `./sts crystal-sphere`
- `./sts overlay`
- `./sts wait player-ready`
- `./sts exec proceed --wait-for map`
- `./sts get /api/v1/state/full`

CLI implementation notes:
- `./sts` is a thin launcher that runs `tools/sts-cli/main.ts`
- the CLI uses Node's built-in TypeScript stripping on Node 24+
- run `npm run check` to type-check the CLI implementation
- run `npm run test:cli` to execute the CLI unit tests with Node's built-in test runner
- run `npm run verify:cli` to run both type-checking and the CLI test suite
- `tools/sts-cli/core/` contains shared runtime pieces such as HTTP, JSON handling, errors, and output
- `tools/sts-cli/commands/` contains command-specific logic such as `exec` payload building and `wait`

Higher-level CLI helpers:
- `./sts room summary` returns one combined snapshot for the current actionable room
- `./sts wait player-ready` waits for a stable player-actionable state or player combat turn
- `./sts wait rewards` / `./sts wait map` / `./sts wait monster` wait for a stable matching room state, not just a one-frame context match
- `./sts exec ACTION ... --wait-for CONDITION` executes an action and then blocks until the requested stable follow-up state is reached
- `./sts rewards claim-all-safe` automatically claims deterministic non-card rewards and stops before card choice

Local development helpers:
- `./restart_game.sh --wait-for-server` restarts the game and waits for the observer server to respond
- `./dev_cycle.sh` runs the full build -> deploy -> restart -> wait loop
- `./dev_cycle.sh --smoke` adds a basic `./sts ping`, `./sts context`, and `./sts actions` verification pass after restart

Transition handling:
- `./sts context` is the first place to check whether a screen is actionable
- if `isTransitioning=true`, treat the current frame as non-actionable and wait
- `/api/v1/actions` is intentionally empty during known transient states so agents do not fire against half-settled UI

Current low-token combat flow:
- `context` -> current scene classification
- `compact observation` -> minimal decision facts
- `combat/summary` -> round, pile counts, incoming damage
- `actions` -> current-screen legal actions with parameters and semantic hints
- `combat/enemies` -> target and threat details only when needed
- `knowledge/current` -> current card / relic / potion / status text cache only when ids need expansion
- `delta observation` -> use after actions to avoid re-reading unchanged sections

Recommended execution flow:
- `context` -> confirm `isStable=true`
- `actions` -> pick one legal action
- `exec ... --wait-for ...` -> execute and wait for the next stable state when a transition is expected
- `delta observation` or `room summary` -> inspect the new stable state

The server is intentionally split into small read-only endpoints so an agent can query only the state it needs instead of re-reading a full run snapshot every time.

## Phase 2 Start

With Phase 1 closed, the next work moves to Phase 2 action execution:

- executable action APIs for combat, map, rewards, shop, rest-site, event, and selection screens
- legality checks and recovery around screen transitions and target invalidation
- multi-step interaction handling for targeted cards, card selection, relic selection, and proceed flows
- action result logging linked to observation deltas

Current implemented Phase 2 surface:

- `play_card`
- `use_potion`
- `discard_potion`
- `end_turn`
- `choose_map_node`
- `choose_event_option`
- `advance_dialogue`
- `choose_rest_option`
- `shop_purchase`
- `claim_reward`
- `select_card_reward`
- `skip_card_reward`
- `proceed`
- `select_card`
- `confirm_selection`
- `cancel_selection`
- `skip_selection`
- `select_bundle`
- `confirm_bundle_selection`
- `cancel_bundle_selection`
- `select_relic`
- `skip_relic_selection`
- `crystal_sphere_set_tool`
- `crystal_sphere_click_cell`
- `crystal_sphere_proceed`
- `claim_treasure_relic`

Validated in live play:

- full combat turn execution across multiple turns
- reward claiming
- card reward selection
- map node selection
- stable `wait` handling across combat turns and room transitions
- `exec --wait-for` returning a settled follow-up state after transient frames

Canonical execution contract:

- request shape: `action` + optional `index` + optional `target`
- combat target selection uses `target`
- every indexed action now accepts the same canonical `index`
- legacy aliases like `card_index`, `slot`, `reward_index`, and `relic_index` are still accepted for compatibility

See `docs/phase-2-todo.md` for the concrete task list.

The next planning-layer work is tracked in `docs/phase-3-todo.md`.

## Build For Local macOS Game
```bash
./build_and_deploy.sh
```

This deploys the mod into `mods/taku_agent/` with:
- `mod_manifest.json`
- `taku_agent.pck`
- `taku_agent.dll`

## Restart Local Game
```bash
./restart_game.sh --wait-for-server
```

Useful options:
- `--server-timeout 120`
- `--no-force-kill`

Environment overrides:
- `STS2_APP_PATH`
- `STS2_GAME_BIN`
- `STS_OBSERVER_URL`
- `STS2_STEAM_APP_ID`
- `STS2_LAUNCH_DIRECT=1`

By default `restart_game.sh` launches the game through Steam with `steam://run/2868840`, which avoids Steam session and ownership errors that can happen when opening the `.app` bundle directly.

## Full Development Cycle
```bash
./dev_cycle.sh
```

Optional smoke run:
```bash
./dev_cycle.sh --smoke
```

Useful options:
- `--server-timeout 120`
- `--skip-build`
- `--skip-restart`

## Build Release Package
```bash
./build_release.sh
```

The project is scaffolded from the working structure used in `deadly_nuke_mod/`, but starts with a minimal empty mod initializer so you can add cards, patches, relics, or events incrementally.
