# STS2 Taku Agent

Starter repository for a Slay the Spire 2 mod focused on building an AI-playable interface for Slay the Spire 2.

## Planning Docs
- `docs/overall-plan.md`: project roadmap across the three major phases
- `docs/phase-1-observer.md`: detailed plan for building the game-state observation layer
- `docs/phase-1-api.md`: low-token API design for phase 1 observation
- `docs/feasibility/README.md`: feasibility assessment for the three planned phases

## Repository Layout
- `src/`: C# mod source
- `pack/`: files packaged into the `.pck`
- `tools/`: helper scripts for packaging
- `build_and_deploy.sh`: build locally and copy outputs into the game `mods` folder
- `build_release.sh`: build a shareable release package

## Current Phase 1 Status
The repository now includes a working battle-state capture pipeline for in-combat observation.

It also now exposes a local read-only observation server designed for low-token LLM access, plus a repo-local CLI for stable local querying.

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
- Fine-grained read-only endpoints for combat, player, map, rewards, event, shop, rest-site, treasure, and card selection
- `combat/actions` endpoint exposing legal actions and legal target sets
- Lightweight `player/summary` endpoint without full deck payload
- Structured combat summary with `incomingDamage` and `playableCards`
- Repo-local `./sts` CLI wrapper over the observation server

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
- `http://localhost:15527/api/v1/combat/actions`

Local CLI:
- `./sts ping`
- `./sts next`
- `./sts combat actions`
- `./sts player summary`
- `./sts get /api/v1/state/full`

Current low-token combat flow:
- `context` -> current scene classification
- `compact observation` -> minimal decision facts
- `combat/summary` -> round, pile counts, incoming damage
- `combat/actions` -> legal actions and legal target sets
- `combat/hand` -> card text only when deeper reasoning is needed

The server is intentionally split into small read-only endpoints so an agent can query only the state it needs instead of re-reading a full run snapshot every time.

## Next Work

The next Phase 1 tasks are:

- add richer semantic fields to `combat/actions`
  for example estimated damage, block gain, status application, and resource spend
- add `delta observation`
  so post-action reads return only what changed instead of another full subtree
- extend the same low-token action-layer design to map, reward, shop, and event screens
- separate static knowledge from dynamic state more aggressively
  for example card / relic / potion dictionaries instead of repeating long descriptions
- define the smallest stable observation contract that Phase 2 action APIs can depend on

## Build For Local macOS Game
```bash
./build_and_deploy.sh
```

This deploys the mod into `mods/taku_agent/` with:
- `mod_manifest.json`
- `taku_agent.pck`
- `taku_agent.dll`

## Build Release Package
```bash
./build_release.sh
```

The project is scaffolded from the working structure used in `deadly_nuke_mod/`, but starts with a minimal empty mod initializer so you can add cards, patches, relics, or events incrementally.
