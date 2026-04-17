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

It also now exposes a local read-only observation server designed for low-token LLM access.

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
