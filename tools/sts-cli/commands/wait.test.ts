import test from "node:test";
import assert from "node:assert/strict";

import { normalizeWaitCondition, waitForCondition } from "./wait.ts";
import { MockClient } from "../test-helpers/mock-client.ts";

test("normalizeWaitCondition lowercases and normalizes dashes", () => {
  assert.equal(normalizeWaitCondition("Player-Turn"), "player_turn");
});

test("waitForCondition resolves immediately for matching stateType", async () => {
  const client = new MockClient({
    "/api/v1/context": {
      stateType: "rewards",
      roomType: "Monster"
    }
  });

  const result = await waitForCondition(client, "rewards", 1);
  assert.equal(result.condition, "rewards");
  assert.equal(result.context.stateType, "rewards");
  assert.equal(client.requests.length, 1);
});

test("waitForCondition reads combat summary for player_turn", async () => {
  const client = new MockClient({
    "/api/v1/context": {
      stateType: "monster",
      roomType: "Monster"
    },
    "/api/v1/combat/summary": {
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
  });

  const result = await waitForCondition(client, "player_turn", 1);
  assert.equal(result.combat?.side, "player");
  assert.deepEqual(
    client.requests.map((entry) => entry.path),
    ["/api/v1/context", "/api/v1/combat/summary"]
  );
});
