import test from "node:test";
import assert from "node:assert/strict";

import { normalizeWaitCondition, waitForCondition } from "./wait.ts";
import { MockClient } from "../test-helpers/mock-client.ts";

test("normalizeWaitCondition lowercases and normalizes dashes", () => {
  assert.equal(normalizeWaitCondition("Player-Turn"), "player_turn");
});

test("waitForCondition resolves after a stable matching stateType", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "rewards",
        roomType: "Monster"
      },
      {
        stateType: "rewards",
        roomType: "Monster"
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "rewards",
        actions: [
          { actionType: "proceed", label: "Proceed" }
        ]
      },
      {
        stateType: "rewards",
        actions: [
          { actionType: "proceed", label: "Proceed" }
        ]
      }
    ]
  });

  const result = await waitForCondition(client, "rewards", 1);
  assert.equal(result.condition, "rewards");
  assert.equal(result.context.stateType, "rewards");
  assert.deepEqual(
    client.requests.map((entry) => entry.path),
    ["/api/v1/context", "/api/v1/actions", "/api/v1/context", "/api/v1/actions"]
  );
});

test("waitForCondition reads combat summary for player_turn", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "monster",
        roomType: "Monster"
      },
      {
        stateType: "monster",
        roomType: "Monster"
      }
    ],
    "/api/v1/combat/summary": [
      {
        roomType: "monster",
        round: 2,
        side: "player",
        handCount: 5,
        enemyCount: 1,
        incomingDamage: 7,
        playableCards: 4,
        potionActions: 0,
        actionCount: 5,
        piles: {
          draw: 3,
          discard: 5,
          exhaust: 0
        }
      },
      {
        roomType: "monster",
        round: 2,
        side: "player",
        handCount: 5,
        enemyCount: 1,
        incomingDamage: 7,
        playableCards: 4,
        potionActions: 0,
        actionCount: 5,
        piles: {
          draw: 3,
          discard: 5,
          exhaust: 0
        }
      }
    ]
  });

  const result = await waitForCondition(client, "player_turn", 1);
  assert.equal(result.combat?.side, "player");
  assert.deepEqual(
    client.requests.map((entry) => entry.path),
    ["/api/v1/context", "/api/v1/combat/summary", "/api/v1/context", "/api/v1/combat/summary"]
  );
});

test("waitForCondition ignores stale player_turn combat snapshots with zero actions", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "monster",
        roomType: "Monster"
      },
      {
        stateType: "monster",
        roomType: "Monster"
      },
      {
        stateType: "monster",
        roomType: "Monster"
      }
    ],
    "/api/v1/combat/summary": [
      {
        roomType: "monster",
        round: 2,
        side: "player",
        handCount: 0,
        enemyCount: 2,
        incomingDamage: 6,
        playableCards: 0,
        potionActions: 0,
        actionCount: 0,
        piles: {
          draw: 10,
          discard: 5,
          exhaust: 0
        }
      },
      {
        roomType: "monster",
        round: 2,
        side: "player",
        handCount: 5,
        enemyCount: 2,
        incomingDamage: 6,
        playableCards: 5,
        potionActions: 0,
        actionCount: 6,
        piles: {
          draw: 10,
          discard: 5,
          exhaust: 0
        }
      },
      {
        roomType: "monster",
        round: 2,
        side: "player",
        handCount: 5,
        enemyCount: 2,
        incomingDamage: 6,
        playableCards: 5,
        potionActions: 0,
        actionCount: 6,
        piles: {
          draw: 10,
          discard: 5,
          exhaust: 0
        }
      }
    ]
  });

  const result = await waitForCondition(client, "player_turn", 1);
  assert.equal(result.combat?.actionCount, 6);
});

test("waitForCondition requires two stable matching rewards observations", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "rewards",
        roomType: "Monster",
        overlayType: "NRewardsScreen"
      },
      {
        stateType: "map",
        roomType: "Monster"
      },
      {
        stateType: "rewards",
        roomType: "Monster",
        overlayType: "NRewardsScreen"
      },
      {
        stateType: "rewards",
        roomType: "Monster",
        overlayType: "NRewardsScreen"
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "rewards",
        actions: [
          { actionType: "proceed", label: "Proceed" }
        ]
      },
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Monster" }
        ]
      },
      {
        stateType: "rewards",
        actions: [
          { actionType: "proceed", label: "Proceed" }
        ]
      },
      {
        stateType: "rewards",
        actions: [
          { actionType: "proceed", label: "Proceed" }
        ]
      }
    ]
  });

  const result = await waitForCondition(client, "rewards", 1);
  assert.equal(result.context.stateType, "rewards");
  assert.deepEqual(
    client.requests.map((entry) => entry.path),
    [
      "/api/v1/context",
      "/api/v1/actions",
      "/api/v1/context",
      "/api/v1/actions",
      "/api/v1/context",
      "/api/v1/actions",
      "/api/v1/context",
      "/api/v1/actions"
    ]
  );
});

test("waitForCondition requires primary actions for player_ready map states", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "map",
        roomType: "Monster"
      },
      {
        stateType: "map",
        roomType: "Monster"
      },
      {
        stateType: "map",
        roomType: "Monster"
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "map",
        actions: [
          { actionType: "discard_potion", label: "Discard potion" }
        ]
      },
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Unknown" }
        ]
      },
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Unknown" }
        ]
      }
    ]
  });

  const result = await waitForCondition(client, "player_ready", 1);
  assert.equal(result.context.stateType, "map");
});

test("waitForCondition supports stable combat stateType waits", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "monster",
        roomType: "Monster"
      },
      {
        stateType: "monster",
        roomType: "Monster"
      }
    ],
    "/api/v1/combat/summary": [
      {
        roomType: "monster",
        round: 1,
        side: "player",
        handCount: 5,
        enemyCount: 1,
        incomingDamage: 3,
        playableCards: 5,
        potionActions: 0,
        actionCount: 6,
        piles: {
          draw: 10,
          discard: 0,
          exhaust: 0
        }
      },
      {
        roomType: "monster",
        round: 1,
        side: "player",
        handCount: 5,
        enemyCount: 1,
        incomingDamage: 3,
        playableCards: 5,
        potionActions: 0,
        actionCount: 6,
        piles: {
          draw: 10,
          discard: 0,
          exhaust: 0
        }
      }
    ]
  });

  const result = await waitForCondition(client, "monster", 1);
  assert.equal(result.context.stateType, "monster");
  assert.equal(result.combat?.roomType, "monster");
});

test("waitForCondition supports stable resumed-run waits after continue_game", async () => {
  const client = new MockClient({
    "/api/v1/context": [
      {
        stateType: "menu",
        isStable: true,
        isTransitioning: false
      },
      {
        stateType: "map",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      },
      {
        stateType: "map",
        roomType: "Monster",
        isStable: true,
        isTransitioning: false
      }
    ],
    "/api/v1/actions": [
      {
        stateType: "menu",
        actions: [
          { actionType: "continue_game", label: "Continue game" }
        ]
      },
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Monster" }
        ]
      },
      {
        stateType: "map",
        actions: [
          { actionType: "choose_map_node", label: "Node 0: Monster" }
        ]
      }
    ]
  });

  const result = await waitForCondition(client, "run_active", 1);
  assert.equal(result.context.stateType, "map");
});
