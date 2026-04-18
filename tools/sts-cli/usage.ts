export const usage = `Usage:
  sts help
  sts ping
  sts capabilities
  sts context
  sts menu
  sts next
  sts compact
  sts delta
  sts actions
  sts run
  sts run snapshot
  sts knowledge [current|cards|relics|potions|status]
  sts player [summary|deck|relics|potions|status]
  sts combat [summary|actions|hand|enemies|piles|snapshot]
  sts map
  sts event
  sts fake-merchant
  sts shop
  sts rest-site
  sts rewards
  sts rewards claim-all-safe
  sts card-reward
  sts card-reward skip
  sts card-selection
  sts bundle-selection
  sts relic-selection
  sts crystal-sphere
  sts treasure
  sts overlay
  sts logs tail [--file action-execution|action-history] [--last N]
  sts logs correlation CORRELATION_ID [--last N]
  sts doctor
  sts wait CONDITION [TIMEOUT_SECONDS] [--verbose]
  sts wait CONDITION --timeout SECONDS [--verbose]
  sts wait room-ready [TIMEOUT_SECONDS]
  sts wait run-active [TIMEOUT_SECONDS]
  sts wait player-ready [TIMEOUT_SECONDS]
  sts room summary
  sts room snapshot [--detail standard|full]
  sts exec ACTION [INDEX] [TARGET]
  sts exec ACTION [key=value ...]
  sts exec ACTION ... [--wait-for CONDITION] [--timeout SECONDS]
  sts exec ACTION ... [--wait-for-ready|--wait-for-room|--wait-for-run] [--wait-verbose]
  sts full
  sts get /api/v1/...

Environment:
  STS_OBSERVER_URL   Override observer server base URL.

Examples:
  sts ping
  sts next
  sts menu
  sts actions
  sts run snapshot
  sts combat snapshot
  sts combat actions
  sts player summary
  sts room summary
  sts room snapshot --detail full
  sts knowledge cards
  sts bundle-selection
  sts relic-selection
  sts crystal-sphere
  sts logs tail --last 5
  sts logs correlation 1234abcd
  sts doctor
  sts wait player_turn
  sts wait room-ready --verbose
  sts wait run-active
  sts wait player-ready
  sts wait rewards 10
  sts rewards claim-all-safe
  sts card-reward skip
  sts exec open_treasure
  sts exec play_card 0 jaw_worm_0
  sts exec select_card 1
  sts exec end_turn
  sts exec proceed
  sts exec proceed --wait-for map
  sts exec end_turn --wait-verbose
  sts exec continue_game --wait-for-run --wait-verbose --timeout 30
  sts exec continue_game --wait-for run_active --timeout 30
  sts exec end_turn --wait-for player_turn --timeout 10
  sts get /api/v1/state/full`;
