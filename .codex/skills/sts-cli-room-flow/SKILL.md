---
name: sts-cli-room-flow
description: Use when the game is in a non-combat actionable room and you need to progress the run through `sts`. Covers menu resume, map routing, rewards, card rewards, events, shops, rest sites, treasure, and proceed-style room transitions.
---

# STS CLI Room Flow

Use this skill to advance stable non-combat rooms without over-querying.

## Default Flow

1. Check `sts context`.
2. If `stateType=menu`, read `sts menu` and use `sts exec continue_game`.
3. Otherwise read `sts room summary` or the narrow room endpoint.
4. Use `sts actions` to pick a legal move.
5. Execute with `sts exec ...` and rely on built-in room waits when available.

## Common Commands

- Menu resume: `sts menu`, `sts exec continue_game`
- Map: `sts map`, `sts exec choose_map_node index=N`
- Rewards: `sts rewards`, `sts rewards claim-all-safe`, `sts exec claim_reward index=N`
- Card rewards: `sts card-reward`, `sts card-reward skip`, `sts exec select_card_reward index=N`, `sts exec skip_card_reward`
- Events: `sts event`, `sts exec choose_event_option index=N`, `sts exec advance_dialogue`
- Shops: `sts shop`, `sts fake-merchant`, `sts exec shop_purchase index=N`, `sts exec proceed`
- Rest sites: `sts rest-site`, `sts exec choose_rest_option index=N`, `sts exec proceed`
- Treasure and relic selection: `sts treasure`, `sts relic-selection`, `sts exec claim_treasure_relic index=N`, `sts exec select_relic index=N`

## Transition Rules

- `sts exec continue_game` defaults to waiting for `run_active`.
- `sts exec choose_map_node ...` and `sts exec proceed` default to waiting for `room_ready`.
- If you need a specific next state instead of the default, pass `--wait-for ...` explicitly.
- Use `sts wait room-ready --verbose` if a room transition looks stuck.

## Efficiency Rules

- Prefer `sts rewards claim-all-safe` before card rewards.
- Do not assume card rewards are mandatory. When `canSkip=true`, keep `skip_card_reward` as an explicit legal candidate.
- Use one room-specific endpoint instead of multiple unrelated reads.
- Only read `sts player summary` or `sts knowledge current` when the room decision depends on build context.
