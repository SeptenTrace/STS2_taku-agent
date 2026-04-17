export const usage = `Usage:
  ./sts help
  ./sts ping
  ./sts capabilities
  ./sts context
  ./sts next
  ./sts compact
  ./sts delta
  ./sts actions
  ./sts run
  ./sts knowledge [current|cards|relics|potions|status]
  ./sts player [summary|deck|relics|potions|status]
  ./sts combat [summary|actions|hand|enemies|piles]
  ./sts map
  ./sts event
  ./sts shop
  ./sts rest-site
  ./sts rewards
  ./sts rewards claim-all-safe
  ./sts card-reward
  ./sts card-selection
  ./sts bundle-selection
  ./sts relic-selection
  ./sts treasure
  ./sts overlay
  ./sts wait CONDITION [TIMEOUT_SECONDS]
  ./sts wait player-ready [TIMEOUT_SECONDS]
  ./sts room summary
  ./sts exec ACTION [INDEX] [TARGET]
  ./sts exec ACTION [key=value ...]
  ./sts full
  ./sts get /api/v1/...

Environment:
  STS_OBSERVER_URL   Override observer server base URL.

Examples:
  ./sts ping
  ./sts next
  ./sts actions
  ./sts combat actions
  ./sts player summary
  ./sts room summary
  ./sts knowledge cards
  ./sts bundle-selection
  ./sts relic-selection
  ./sts wait player_turn
  ./sts wait player-ready
  ./sts wait rewards 10
  ./sts rewards claim-all-safe
  ./sts exec play_card 0 jaw_worm_0
  ./sts exec select_card 1
  ./sts exec end_turn
  ./sts get /api/v1/state/full`;
