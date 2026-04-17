# CLI Reliability Checklist

This checklist captures the concrete reliability gaps found during live CLI-driven gameplay and the implementation work needed to close them.

## Immediate fixes

- [x] Hide combat actions when the player cannot legally act.
  Acceptance:
  `combat/actions` and `/api/v1/actions` must not advertise `end_turn`, card plays, or potion actions during enemy turns, disabled-action transitions, or after the player dies.

- [x] Wait for map travel to land in the destination room before returning an execution result.
  Acceptance:
  `choose_map_node` should keep polling until the map is no longer in the zero-options travel transition, so the returned `context` reflects the destination room instead of a transient map state.

- [x] Remove stale overlay metadata from room-owned states.
  Acceptance:
  `context.overlayType` should only be populated when an overlay is the primary active state, not when a dismissed rewards/card screen is still lingering in the overlay stack.

- [x] Preserve structured HTTP errors in the repo-local CLI.
  Acceptance:
  Read and write failures should print the JSON error payload and HTTP status instead of collapsing to a bare `curl: (22)` message.

- [x] Add a first-class wait primitive to the CLI.
  Acceptance:
  `./sts wait ...` should let agents block on `player_turn`, `enemy_turn`, or a named `stateType` such as `rewards`, `map`, or `monster`.

## Next backlog

- [ ] Add stable action identifiers for hand cards and current-screen actions so agents can execute actions without re-reading shifting indices after every play.
- [ ] Enrich error payloads with the current `context` and recommended next queries for read-only endpoints that return `409`.
- [ ] Add a higher-level `autoplay` room driver that wraps the current query/decide/execute loop for one combat or one room.
