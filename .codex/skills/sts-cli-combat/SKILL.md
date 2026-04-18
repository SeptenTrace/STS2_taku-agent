---
name: sts-cli-combat
description: Use when the current Slay the Spire 2 room is combat and you need to choose and execute legal battle actions through `sts`. Covers combat reads, target selection, potion usage, `play_card`, and turn flow with the CLI's built-in wait behavior.
---

# STS CLI Combat

Use this skill for combat-only decision and execution loops.

## Combat Loop

1. Confirm combat with `sts context` or `sts room summary`.
2. Read `sts actions` first because it already contains legal combat actions and target options.
3. Expand only what is missing:
   `sts combat enemies` for targeting,
   `sts combat hand` for raw card text,
   `sts knowledge current` if ids need expansion.
4. Execute one action with `sts exec ...`.
5. Re-read via `sts room summary`, `sts delta`, or another narrow endpoint.
6. If combat ends on `card_reward`, hand control to room-flow logic and remember that `skip_card_reward` is a legal follow-up action.

## Preferred Commands

- `sts room summary`
- `sts actions`
- `sts combat summary`
- `sts combat enemies`
- `sts combat hand`
- `sts exec play_card INDEX TARGET`
- `sts exec use_potion index=SLOT target=ENTITY`
- `sts exec discard_potion index=SLOT`
- `sts exec end_turn`

## Action Rules

- Trust `sts actions` for legality and target options.
- Use positional form for cards when possible: `sts exec play_card 1 jaw_worm_0`.
- `sts exec end_turn` already defaults to waiting for `player_turn`.
- If a combat card or potion needs a target, pass the entity id exactly as exposed.
- If state sync looks wrong, run `sts doctor` before retrying actions.

## Failure Recovery

- `HTTP 409` plus a combat context usually means the room moved or the side flipped.
- Re-read `sts context` and `sts actions` before retrying.
- If the transition looks flaky, use `sts wait player_turn --verbose` or `sts wait room-ready --verbose`.
